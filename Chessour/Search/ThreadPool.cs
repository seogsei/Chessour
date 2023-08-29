using System.Collections.Generic;

namespace Chessour.Search
{
    internal sealed class ThreadPool : List<Thread>
    {
        public ThreadPool(int initialSize)
        {
            SetSize(initialSize);
        }

        public MasterThread Master => (MasterThread)this[0];

        public ulong TotalNodesSearched()
        {
            ulong total = 0;
            foreach (var thread in this)
                total += thread.searcher.NodeCount;

            return total;
        }

        public void WaitForSearchFinish()
        {
            Master.WaitForSearchFinish();
        }

        public void SetSize(int expected)
        {
            //Remove every thread
            while (Count > 0)
            {
                WaitForSearchFinish();

                while (Count > 0)
                {
                    this[Count - 1].Abort();
                    RemoveAt(Count - 1);
                }
            }

            //Add as many threads as wanted
            if (expected > 0)
            {
                Add(new MasterThread());

                while (Count < expected)
                    Add(new Thread());
            }
        }
    }
}