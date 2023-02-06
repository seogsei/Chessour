using System.Threading;
using static Chessour.Search.DepthConstants;

namespace Chessour.Search;

internal partial class SearchThread
{
    public readonly RootMoves rootMoves;
    private readonly Thread thread;
    private readonly object syncPrimitive;

    private readonly Position position;
    private readonly Position.StateInfo rootState;
    private readonly Position.StateInfo[] states;

    public SearchThread()
    {
        position = new Position(UCI.StartFEN, rootState = new());
        rootMoves = new();
        states = new Position.StateInfo[MAX_PLY];
        for(int i = 0; i< MAX_PLY; i++)
        {
            states[i] = new();
        }

        syncPrimitive = new();
        Searching = true;

        thread = new Thread(IdleLoop);
        thread.Start();
        WaitForSearchFinish();
    }

    public bool Exit { get; set; }

    public void Abort()
    {
        Exit = true;
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
        lock (syncPrimitive)
        {
            if(Searching)
                Monitor.Wait(syncPrimitive);
        }
    }
       
    protected virtual void StartSearch()
    {
        Search();
    }

    private void IdleLoop()
    {
        while (true)
        {
            lock (syncPrimitive)
            {
                Searching = false;

                Monitor.Pulse(syncPrimitive);
                Monitor.Wait(syncPrimitive);

                if (Exit)
                    break;
            }

            StartSearch();
        }
    }
}
