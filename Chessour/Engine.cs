using Chessour.Search;

namespace Chessour
{
    internal static class Engine
    {
        public const string Name = "Chessour";
        public const string Author = "Muhammed Ikbal Yaman";

        static Engine()
        {
            Stop = true;

            _timer = new();
            _timer.Start();

            _threads = new ThreadPool(1);
            _timeManager = new();
            _transpositionTable = new();
        }

        private static readonly Stopwatch _timer;
        private static readonly TimeManager _timeManager;
        private static readonly TranspositionTable _transpositionTable;
        private static readonly ThreadPool _threads;
        private static Limits _limits;
        private static bool _stop;

        public static long Now
        {
            get => _timer.ElapsedMilliseconds;
        }
        public static bool Stop
        {
            get => _stop;
            set => _stop = value;
        }
        public static ThreadPool Threads
        {
            get => _threads;
        }
        public static TranspositionTable TTTable
        {
            get => _transpositionTable;
        }
        public static Limits SearchLimits
        {
            get => _limits;
        }
        public static TimeManager TimeManager
        {
            get => _timeManager;
        }

        public static void StartThinking(Position position, in Limits limits)
        {
            _threads.Master.WaitForSearchFinish();

            Stop = false;
            _limits = limits;
            _timeManager.Initialize(limits, position.ActiveColor, 0);

            foreach (var th in Threads)
            {
                th.SetPosition(position);
                th.ResetSearchStats();
            }

            _threads.Master.Release();
        }
    }
}
