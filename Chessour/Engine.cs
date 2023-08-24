using Chessour.Search;

namespace Chessour
{
    internal static class Engine
    {
        public const string Name = "Chessour";
        public const string Author = "Muhammed Ikbal Yaman";

        static Engine()
        {
            timer.Start();
        }

        private static readonly Stopwatch timer = new();

        public static ThreadPool Threads { get; private set; } = new(1);
        public static TranspositionTable TTTable { get; private set; } = new();
        public static TimeManager TimeManager { get; private set; } = new();
        public static Limits SearchLimits { get; private set; }
        public static long Now => timer.ElapsedMilliseconds;

        public static void StartThinking(Position position, in Limits limits)
        {
            Threads.Master.WaitForSearchFinish();

            SearchLimits = limits;
            TimeManager.Initialize(limits, position.ActiveColor, 0);

            foreach (var th in Threads)
            {
                th.SetPosition(position, limits.Moves);
                th.ResetSearchStats();
            }

            SearchThread.Stop = false;

            Threads.Master.Release();
        }
    }
}
