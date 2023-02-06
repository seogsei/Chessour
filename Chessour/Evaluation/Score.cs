namespace Chessour.Evaluation
{
    public readonly struct Score
    {
        private readonly short midGame;
        private readonly short endGame;

        public Score(Value midGame, Value endGame)
        {
            this.midGame = (short)midGame;
            this.endGame = (short)endGame;
        }

        public Value MidGame { get => midGame; }
        public Value EndGame { get => endGame; }

        public static readonly Score Zero = default;

        public static Score operator +(Score lhs, Score rhs)
        {
            return new Score(lhs.midGame + rhs.midGame, lhs.endGame + rhs.endGame);
        }
        public static Score operator -(Score lhs, Score rhs)
        {
            return new Score(lhs.midGame - rhs.midGame, lhs.endGame - rhs.endGame);
        }
        public static Score operator -(Score score)
        {
            return new Score(-score.midGame, -score.endGame);
        }
    }
}
