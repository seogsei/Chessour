using Chessour.Search;

namespace Chessour
{
    internal static class Engine
    {
        public const string Name = "Chessour";
        public const string Author = "Muhammed Ikbal Yaman";

        static Engine()
        {
            Threads = new(1);

            TranspositionTable = new(128);

            Timer = new();
        }

        public static bool Stop { get; set; }
        public static ThreadPool Threads { get; private set; }
        public static TranspositionTable TranspositionTable { get; private set; }
        public static TimeManager Timer { get; private set; }
        public static UCI.GoParameters SearchLimits { get; private set; }
        public static bool PonderMode { get; set; }

        public static void StartThinking(Position position, in UCI.GoParameters limits, bool ponder)
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
}
