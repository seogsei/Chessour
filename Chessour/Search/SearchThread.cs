using System.Threading;
using static Chessour.Search.DepthConstants;

namespace Chessour.Search;

internal partial class SearchThread
{
    public SearchThread()
    {
        states = new Position.StateInfo[MAX_PLY];
        for (int i = 0; i < MAX_PLY; i++)
            states[i] = new();

        position = new Position(UCI.StartFEN, rootState = new());

        rootMoves = new();

        syncPrimitive = new();

        Searching = true;
        thread = new(Loop)
        {
            IsBackground = true
        };
        thread.Start();

        WaitForSearchFinish();
    }

    private readonly Thread thread;
    private readonly object syncPrimitive;
    private bool abort;

    private readonly Position position;
    private readonly Position.StateInfo rootState;
    private readonly Position.StateInfo[] states;

    public static bool Stop { get; set; } = true;

    public void Abort()
    {
        Debug.Assert(!Searching);

        abort = true;
        Release();
        thread.Join();
    }

    public void Release()
    {
        lock (syncPrimitive)
        {
            Searching = true;
            Monitor.Pulse(syncPrimitive);
        }
    }

    public void WaitForSearchFinish()
    {
        if (Searching)
        {
            lock (syncPrimitive)
            {
                Monitor.Wait(syncPrimitive);
            }
        }
    }

    public virtual void Work()
    {
        Search();
    }

    private void Loop()
    {
        while (true)
        {
            Searching = false;

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
}
