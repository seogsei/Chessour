using Chessour.Search;

namespace Chessour;

internal static class Engine
{
    public const string Name = "Chessour";
    public const string Author = "Muhammed Ikbal Yaman";

    public static bool Stop { get; set; }
    public static bool PonderMode { get; set; }
    public static UCI.GoParameters SearchLimits { get; private set; }
    public static ThreadPool Threads { get; } = new(1);
    public static TranspositionTable TranspositionTable { get; } = new();
    public static TimeManager Timer { get; } = new();

    public static void Run(string[] args)
    {
        UCI.Loop(args);
    }

    public static void StartThinking(Position position, UCI.GoParameters limits, bool ponder)
    {
        Threads.Master.WaitForSearchFinish();

        Stop = false;

        SearchLimits = limits;
        Timer.Initialize(position.ActiveColor, limits);

        foreach (var thread in Threads)
            thread.searcher.SetSearchParameters(position, limits.moves);

        PonderMode = ponder;

        Threads.Master.Release();
    }

    internal static void NewGame()
    {
        Threads.Master.WaitForSearchFinish();

        TranspositionTable.Clear();

        foreach (var thread in Threads)
            thread.Clear();
    }
}
