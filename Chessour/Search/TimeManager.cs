namespace Chessour.Search
{
    internal class TimeManager
    {
        public long StartTime { get; protected set; }
        public long OptimumTime { get; protected set; }
        public long MaxTime { get; protected set; }

        public void Initialize(Color side, UCI.GoParameters parameters)
        {
            const double optScale = 1d / 100;
            const double maxScale = 1d / 20;

            long time = side == Color.White ? parameters.WhiteTime : parameters.BlackTime;
            long increment = side == Color.White ? parameters.WhiteIncrement : parameters.BlackIncrement; ;

            StartTime = parameters.StartTime;

            int movesToGo = parameters.MovesToGo == 0 ? 40 : Math.Min(parameters.MovesToGo, 40);

            long timeLeft = Math.Max(1, time + (increment * (movesToGo - 1)));

            OptimumTime = (long)(timeLeft * optScale);
            MaxTime = (long)(timeLeft * maxScale);
        }

        public long Elapsed()
        {
            return Now() - StartTime;
        }

        static TimeManager()
        {
            stopwatch = new();
            stopwatch.Start();
        }

        private static readonly Stopwatch stopwatch;

        public static long Now()
        {
            return stopwatch.ElapsedMilliseconds;
        }
    }
}