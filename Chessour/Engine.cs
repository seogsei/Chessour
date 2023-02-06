using Chessour.Search;

namespace Chessour
{
    internal static class Engine
    {
        public const string Name = "Chessour";
        public const string Author = "Muhammed Ikbal Yaman";
        private static readonly Stopwatch stopwatch;

        static Engine()
        {
            Stop = true;

            stopwatch = new();
            stopwatch.Start();

            Threads = new ThreadPool(1);
            Time = new();
            TTTable = new TranspositionTable();
        }

        public static long Now
        {
            get
            {
                return stopwatch.ElapsedMilliseconds;
            }
        }


        public static bool Stop { get; set; }
        public static ThreadPool Threads { get; }
        public static TranspositionTable TTTable { get; }
        public static Limits SearchLimits { get; private set; }
        public static TimeManager Time { get; }

        public static void StartThinking(Position position, in Limits limits)
        {
            Threads.Master.WaitForSearchFinish();

            Stop = false;
            SearchLimits = limits;
            Time.Initialize(limits, position.ActiveColor, 0);

            foreach (var th in Threads)
            {
                th.SetPosition(position);
                th.ResetSearchStats();
            }

            Threads.Master.Release();
        }
    }
}
