namespace Chessour.Search;

internal class MasterThread : SearchThread
{
    protected override void StartSearch()
    {
        if (Engine.SearchLimits.perft > 0)
        {
            NodeCount = Perft(Engine.SearchLimits.perft);
            return;
        }

        foreach (var th in Engine.Threads)
            if (th != this)
                th.Release();

        //Start searching on this thread
        Search();

        //Wait for other searches to finish
        foreach (var th in Engine.Threads)
            if (th != this)
                th.WaitForSearchFinish();

        //Find the best thread
        SearchThread bestThread = this;

        Console.Write($"bestmove {bestThread.rootMoves[0].pv[0]}");

        if (bestThread.rootMoves[0].pv.Count > 1)
            Console.Write($" ponder {bestThread.rootMoves[0].pv[1]}");

        Console.WriteLine();
    }
}
