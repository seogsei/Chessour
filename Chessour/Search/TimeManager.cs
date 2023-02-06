namespace Chessour.Search;

internal class TimeManager
{
    public long StartTime { get; protected set; }
    public long OptimumTime { get; protected set; }
    public long MaxTime { get; protected set; }

    public long Elapsed
    {
        get
        {
            return Engine.Now - StartTime;
        }
    }

    public void Initialize(Limits limits, Color us, int ply)
    {
        double optScale = 0.005;
        double maxScale = 0.1;

        StartTime = limits.startTime;

        int movesToGo = limits.movesToGo != 0 ? Math.Min(limits.movesToGo, 50) : 50;

        long flatTime = us == Color.White ? limits.whiteTime : limits.blackTime;
        long increment = us == Color.White ? limits.whiteIncrement : limits.blackIncrement;

        long timeLeft = Math.Max(1, flatTime + increment * (movesToGo - 1));

        OptimumTime = (long)(timeLeft * optScale);
        MaxTime = (long)(timeLeft * maxScale);
    }
}
