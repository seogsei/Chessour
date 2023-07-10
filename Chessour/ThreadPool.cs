using Chessour.Search;
using System.Collections.Generic;

namespace Chessour;

internal sealed class ThreadPool : List<SearchThread>
{
    public ThreadPool(int initialSize)
    {
        SetSize(initialSize);
    }

    public MasterThread Master
    {
        get
        {
            return (MasterThread)this[0];
        }
    }

    public ulong NodesSearched
    {
        get
        {
            ulong total = 0;
            foreach (var thread in this)
                total += thread.NodeCount;

            return total;
        }
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
            Add(new MasterThread());

            while (Count < expected)
                Add(new SearchThread());
        }
    }
}
