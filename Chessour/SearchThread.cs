using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Chessour
{
    sealed class SearchPool : List<SearchThread>
    {
        private MasterSearchThread Master { get => (MasterSearchThread)this[0]; } 

        public void Stop()
        {
            Master.Search.Stop = true;
        }

        public void WaitForSeachFinish() 
        {
            Master.WaitForSearchFinish();
        } 
       
        public ulong NodesSearched()
        {
            ulong total = 0;
            foreach (var thread in this)
                total += thread.Nodes;

            return total;
        }

        public SearchPool(int initialSize)
        {
            SetSize(initialSize);
        }

        public void SetSize(int expected)
        {
            while (Count > 0)
            {
                WaitForSeachFinish();

                while (Count > 0)
                {
                    this[Count - 1].Abort();
                    RemoveAt(Count - 1);
                }
            }

            if (expected > 0)
            {
                Add(new MasterSearchThread(this));

                while (Count < expected)
                    Add(new SearchThread());
            }
        }

        public void StartThinking(Position pos, in SearchObject.SearchLimits limits, bool ponder)
        {
            Master.WaitForSearchFinish();

            MoveList moves = new(pos, stackalloc MoveScore[MoveGenerator.MaxMoveCount]);

            foreach (SearchThread th in this)
            {
                th.Search.Stop = false;
                th.Search.limits = limits;
                th.rootDepth = 0;
                th.Search.rootMoves.Clear();
                th.Search.Reset();
                foreach (Move m in moves)
                    if (limits.searchMoves is null || limits.searchMoves.Contains(m))
                        th.Search.rootMoves.Add(m);

                th.RootPosition.Set(pos, th.RootStateInfo);
            }

            Master.Release();
        }
    }

    class SearchThread
    {
        public Position RootPosition { get; }
        public Position.StateInfo RootStateInfo { get; }
        public SearchObject Search { get; }
        public bool Searching { get; private set; }
        public bool Stop { get => Search.Stop; set => Search.Stop = value; }
        public bool Exit { get; private set; }
        public ulong Nodes { get; protected set; }


        public int rootDepth;
        public Move lastBestMove;
        public Value bestValue;

        readonly Thread thread;
        readonly Mutex mutex;

        public SearchThread()
        {
            RootPosition = new Position(RootStateInfo = new Position.StateInfo());
            Search = new();

            mutex = new(false);

            Searching = true;

            thread = new Thread(IdleLoop);
            thread.Start();
            WaitForSearchFinish();
        }

        public void Abort()
        {
            Exit = true;
            Release();
            thread.Join();
        }
       
        public void Release()
        {
            lock (mutex)
            {
                Searching = true;
                Monitor.PulseAll(mutex);
            }
        }
       
        public void WaitForSearchFinish()
        {
            if (Searching)
                lock (mutex)
                {
                    Monitor.Wait(mutex);
                }
        }
       
        private void IdleLoop()
        {
            while (true)
            {
                lock (mutex)
                {
                    Searching = false;

                    Monitor.PulseAll(mutex);

                    Monitor.Wait(mutex);

                    if (Exit)
                        return;
                }

                StartSearch();
            }
        }
       
        protected virtual void StartSearch()
        {
            Value alpha, beta;

            bestValue = alpha = Value.Min;
            beta = Value.Max;

            while (++rootDepth < SearchObject.MaxDepth
                && !Stop
                && !(Search.limits.depth > 0 && rootDepth > Search.limits.depth))
            {
                foreach (var rm in Search.rootMoves)
                    rm.PreviousScore = rm.Score;

                bestValue = Search.Search(NodeType.Root, RootPosition, 0, alpha, beta, rootDepth);

                if (Stop)
                    break;

                Debug.Assert(alpha >= Value.Min && beta <= Value.Max);

                Search.rootMoves.Sort();

                if ((MasterSearchThread)this is not null
                    && !Stop)
                    Console.WriteLine($"info depth {rootDepth} score {UCI.ToString(bestValue)} nodes {Search.NodeCount} qnodes {Search.QNodeCount} pv {UCI.ParsePV(Search.rootMoves.First().pv)}");
            }

            if (Search.rootMoves[0].Move != lastBestMove)
            {
                lastBestMove = Search.rootMoves[0].Move;
            }
        }
    }
    class MasterSearchThread : SearchThread
    {
        readonly SearchPool pool;

        public MasterSearchThread(SearchPool owner) : base()
        {
            pool = owner;
        }

        protected override void StartSearch()
        {
            if (Search.limits.perft > 0)
            {
                Nodes = Search.Perft(RootPosition, Search.limits.perft);

                Console.WriteLine("\nNodes searched: " + Nodes + "\n");
                return;
            }

            if (Search.rootMoves.Count == 0)
            {
                Console.WriteLine("info depth 0 score " + (RootPosition.IsCheck() ? Value.Mated : Value.Draw));
            }
            else
            {
                foreach (var th in pool)
                    if (th != this)
                        th.Release();

                base.StartSearch();
            }

            foreach (var th in pool)
                if (th != this)
                    th.WaitForSearchFinish();

            //Find the best thread
            SearchThread bestThread = this;

            Console.WriteLine($"info bestmove {UCI.ToString(bestThread.Search.rootMoves[0].Move)} ponder {UCI.ToString(bestThread.Search.rootMoves[0].Refutation)}");
        }
    }
}
