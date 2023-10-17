namespace Chessour.Search
{
    internal class TimeManager
    {
        public DateTime StartTime { get; protected set; }
        public TimeSpan OptimumTime { get; protected set; }
        public TimeSpan MaxTime { get; protected set; }

        public void Initialize(Color side, UCI.GoParameters parameters)
        {
            const double optScale = 1d / 100;
            const double maxScale = 1d / 20;

            StartTime = parameters.startTime;

            TimeSpan time = side == Color.White ? parameters.whiteTime : parameters.blackTime;
            TimeSpan increment = side == Color.White ? parameters.whiteIncrement : parameters.blackIncrement; ;

            int movesToGo = parameters.movesToGo == 0 ? 40 : Math.Min(parameters.movesToGo, 40);

            TimeSpan timeLeft = time + (increment * (movesToGo - 1));

            OptimumTime = timeLeft * optScale;
            MaxTime = timeLeft * maxScale;
        }

        public TimeSpan Elapsed()
        {
            return Now() - StartTime;
        }

        static TimeManager()
        {
            stopwatch = new();
            stopwatch.Start();
        }

        private static readonly Stopwatch stopwatch;

        public static DateTime Now()
        {
            return DateTime.UtcNow;
        }
    }
}