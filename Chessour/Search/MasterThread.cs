namespace Chessour.Search;

internal class MasterThread : SearchThread
{
    public override void Work()
    {
        if (Engine.SearchLimits.Perft > 0)
        {
            NodeCount = Perft(Engine.SearchLimits.Perft);
            Console.WriteLine($"Nodes searched : {NodeCount}");
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

        Console.Write($"bestmove {UCI.Move(bestThread.rootMoves[0].Move)}");

        if (bestThread.rootMoves[0].PV.Count > 1)
            Console.Write($" ponder {UCI.Move(bestThread.rootMoves[0].Refutation)}");

        Console.WriteLine();
    }
}
