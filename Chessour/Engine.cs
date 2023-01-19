using System;

namespace Chessour
{
    static class Engine
    {
        public const string Name = "Chessour";
        public const string Author = "Muhammed İkbal Yaman";
        public static SearchPool Threads { get; }

        readonly static Stopwatch timer;

        public static long Now() => timer.ElapsedMilliseconds;

        public static void Init() { }

        static Engine()
        {
            timer = new();
            timer.Start();

            Threads = new SearchPool(1);
        }
    }
}
