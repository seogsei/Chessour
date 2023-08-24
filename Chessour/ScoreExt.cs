namespace Chessour
{
    public readonly struct ScoreExt
    {
        public ScoreExt(int midGame, int endGame)
        {
            MidGame = (short)midGame;
            EndGame = (short)endGame;
        }

        public int MidGame { get; }
        public int EndGame { get; }

        public static readonly ScoreExt Zero = default;

        public static ScoreExt operator +(ScoreExt lhs, ScoreExt rhs)
        {
            return new ScoreExt(lhs.MidGame + rhs.MidGame, lhs.EndGame + rhs.EndGame);
        }

        public static ScoreExt operator -(ScoreExt lhs, ScoreExt rhs)
        {
            return new ScoreExt(lhs.MidGame - rhs.MidGame, lhs.EndGame - rhs.EndGame);
        }

        public static ScoreExt operator -(ScoreExt score)
        {
            return new ScoreExt(-score.MidGame, -score.EndGame);
        }
    }
}