namespace Chessour
{
    internal static class Engine
    {
        public const string Name = "Chessour";
        public const string Author = "Muhammed İkbal Yaman";
        private static readonly Stopwatch timer;

        static Engine()
        {
            timer = new();
            timer.Start();

            Threads = new SearchPool(1);

            TTTable = new TranspositionTable();
        }

        public static SearchPool Threads { get; }
        public static TranspositionTable TTTable { get; }

        public static long Now
        {
            get
            {
                return timer.ElapsedMilliseconds;
            }
        }
    }
}
