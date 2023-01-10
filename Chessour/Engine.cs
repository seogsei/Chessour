using System.Diagnostics;

namespace Chessour
{
    class Engine
    {
        public const string Name = "Chessour";
        public const string Author = "Muhammed İkbal Yaman";

        readonly static Stopwatch timer;
        public static SearchPool Threads { get; }

        public static long Now() => timer.ElapsedMilliseconds;


        static Engine()
        {
            timer = new();
            timer.Start();

            Threads = new SearchPool(1);
        }
    }
}
