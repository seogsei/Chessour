using System.Collections.Generic;
using System.Threading;

namespace Chessour
{
    sealed class SearchPool : List<SearchThread>
    {
        public SearchPool(int initialSize)
        {
            SetSize(initialSize);
        }
        
        public MasterSearchThread Master
        {
            get
            {
                return (MasterSearchThread)this[0];
            }
        }

        public ulong NodesSearched
        {
            get
            {
                ulong total = 0;
                foreach (var thread in this)
                    total += thread.SearchObject.stats.nodeCount;

                return total;
            }
        }
        
        public void Stop()
        {
            Master.SearchObject.stop = true;
        }

        public void WaitForSeachFinish()
        {
            Master.WaitForSearchFinish();
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

        public void StartThinking(Position position, in UCI.SearchLimits limits, bool ponder)
        {
            Master.WaitForSearchFinish();

            foreach (SearchThread th in this)
            {
                th.SearchObject.SetUpSearchParameters(position, in limits);
            }

            Master.Release();
        }
    }

    class SearchThread
    {
        readonly Thread thread;
        readonly Mutex mutex;

        public SearchThread()
        {
            SearchObject = new();

            mutex = new(false);
            Searching = true;

            thread = new Thread(IdleLoop);
            thread.Start();
            WaitForSearchFinish();
        }

        public Search SearchObject { get; }
        public bool Searching { get; private set; }
        public bool Exit { get; private set; }

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
           
        protected virtual void StartSearch()
        {
            SearchObject.StartSearch();
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
    }

    class MasterSearchThread : SearchThread
    {
        readonly SearchPool pool;

        public MasterSearchThread(SearchPool owner) : base()
        {
            pool = owner;
            SearchObject.sendInfo = true;
        }

        protected override void StartSearch()
        {
            if (SearchObject.limits.perft > 0)
            {
                SearchObject.StartSearch();
                return;
            }

            foreach (var th in pool)
                if (th != this)
                    th.Release();

            //Start searching on this thread
            SearchObject.StartSearch();

            //Wait for other searches to finish
            foreach (var th in pool)
                if (th != this)
                    th.WaitForSearchFinish();

            //Find the best thread
            SearchThread bestThread = this;

            Console.WriteLine($"info bestmove {bestThread.SearchObject.rootMoves[0].pv[0].LongAlgebraicNotation()} ponder {bestThread.SearchObject.rootMoves[0].pv[1].LongAlgebraicNotation()}");
        }
    }
}
