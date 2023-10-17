using System.Threading;

namespace Chessour.Search
{
    internal class Thread
    {
        public Thread()
        {
            searcher = new();
            syncPrimitive = new();

            searching = true;
            thread = new(Loop)
            {
                IsBackground = true
            };
            thread.Start();

            WaitForSearchFinish();
        }

        private readonly System.Threading.Thread thread;
        private readonly object syncPrimitive;
        private bool searching;
        private bool abort;

        internal readonly Searcher searcher;

        public void Abort()
        {
            Debug.Assert(!searching);

            abort = true;
            Release();
            thread.Join();
        }

        public void Release()
        {
            lock (syncPrimitive)
            {
                searching = true;
                Monitor.Pulse(syncPrimitive);
            }
        }

        public void WaitForSearchFinish()
        {
            if (searching)
            {
                lock (syncPrimitive)
                {
                    Monitor.Wait(syncPrimitive);
                }
            }
        }

        public virtual void Work()
        {
            searcher.Search();
        }

        private void Loop()
        {
            while (true)
            {
                searching = false;

                lock (syncPrimitive)
                {
                    //Awaken the main thread waiting for the search to finish
                    Monitor.Pulse(syncPrimitive);

                    //Go to sleep until a search is started
                    Monitor.Wait(syncPrimitive);

                    if (abort)
                        return;
                }

                Work();
            }
        }

        internal void Clear()
        {
            searcher.ClearHistoryTables();
        }
    }

    internal class MasterThread : Thread
    {
        public MasterThread() : base()
        {
            searcher.SendInfo = true;
        }

        public override void Work()
        {
            if (Engine.SearchLimits.perft > 0)
            {
                searcher.Perft(Engine.SearchLimits.perft);
                Console.WriteLine($"Nodes searched : {searcher.NodeCount}");
                return;
            }

            foreach (var thread in Engine.Threads)
                if (thread != this)
                    thread.Release();

            //Start searching on this thread
            base.Work();

            //Wait for a stop or ponderhit command
            if (!Engine.Stop && (Engine.PonderMode || Engine.SearchLimits.infinite)) { }

            //Stop the threads
            Engine.Stop = true;

            //Wait for other searches to finish
            foreach (var thread in Engine.Threads)
                if (thread != this)
                    thread.WaitForSearchFinish();

            //Find the best thread
            Searcher bestSearch = this.searcher;

            //Send best move command
            Console.Write($"bestmove {UCI.Move(bestSearch.rootMoves[0].Move)}");

            if (bestSearch.rootMoves[0].PV.Count > 1)
                Console.Write($" ponder {UCI.Move(bestSearch.rootMoves[0].Refutation)}");

            Console.WriteLine();
        }
    }
}