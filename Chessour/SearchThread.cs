using Chessour.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Chessour
{
    sealed class SearchPool : List<SearchThread>
    {
        private MasterSearchThread Main => (MasterSearchThread)this[0];
        public void WaitForSeachFinish() => Main.WaitForSearchFinish();
        public bool Stop
        {
            get => Main.Stop;
            set
            {
                foreach (SearchThread th in this)
                    th.Stop = value;
            }
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

        public void StartThinking(Position pos, in SearchContext.SearchLimits limits, bool ponder)
        {
            Main.WaitForSearchFinish();

            Stop = false;

            MoveList moves = new(pos, stackalloc MoveScore[MoveList.MaxMoveCount]);

            foreach (SearchThread th in this)
            {
                th.Search.limits = limits;
                th.rootDepth = 0;
                th.Search.rootMoves.Clear();
                th.Search.Reset();
                foreach (Move m in moves)
                    if (limits.SearchMoves.Count == 0 || limits.SearchMoves.Contains(m))
                        th.Search.rootMoves.Add(m);

                th.rootPosition.Set(pos, th.rootState);
            }

            Main.Release();
        }
    }

    class SearchThread
    {
        public SearchContext Search { get; }
        public bool SendInfo { get; set; }
        public bool Searching { get; private set; }
        public bool Stop
        {
            get => Search.Stop;
            set => Search.Stop = value;
        }
        public bool Exit { get; private set; }

        public ulong Nodes { get; protected set; }

        public Mutex mutex;
        public Position rootPosition;
        public Position.StateInfo rootState;
        public int rootDepth;
        public Move lastBestMove;
        public Value bestValue;


        readonly Thread thread;

        public SearchThread()
        {
            rootPosition = new Position(rootState = new Position.StateInfo());
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
            lock (mutex)
            {
                if (Searching)
                    Monitor.Wait(mutex);
            }
        }
        void IdleLoop()
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

            while (++rootDepth < SearchContext.MaxDepth
                && !Stop
                && !(Search.limits.Depth > 0 && rootDepth > Search.limits.Depth))
            {
                foreach (var rm in Search.rootMoves)
                    rm.PreviousScore = rm.Score;

                bestValue = Search.Search(NodeType.Root, rootPosition, 0, alpha, beta, rootDepth);

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
            if (Search.limits.Perft > 0)
            {
                Nodes = Search.Perft(rootPosition, Search.limits.Perft);

                Console.WriteLine("\nNodes searched: " + Nodes + "\n");
                return;
            }

            if (Search.rootMoves.Count == 0)
            {
                Console.WriteLine("info depth 0 score " + (rootPosition.IsCheck() ? Value.Mated : Value.Draw));
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
