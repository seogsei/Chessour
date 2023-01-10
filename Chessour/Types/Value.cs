namespace Chessour.Types
{
    public enum Value
    {
        Draw = 0,
        KnownWin = MateInMaxPly - 1,
        MateInMaxPly = Mate - SearchContext.MaxPly,
        Mate = 32000,
        Max = 32001,


        KnownLoss = -KnownWin,
        Mated = -Mate,
        Min = -Max,

        PawnMG = 100, PawnEG = 110,
        KnightMG = 300, KnightEG = 300,
        BishopMG = 300, BishopEG = 300,
        RookMG = 440, RookEG = 480,
        QueenMG = 890, QueenEG = 950,
    }

    public readonly struct Score
    {
        readonly short midGame;
        readonly short endGame;

        public Value MidGame { get => (Value)midGame; }
        public Value EndGame { get => (Value)endGame; }

        public Score(Value midGame, Value endGame)
        {
            this.midGame = (short)midGame;
            this.endGame = (short)endGame;
        }
        public Score(int midGame, int endGame)
        {
            this.midGame = (short)midGame;
            this.endGame = (short)endGame;
        }

        public static Score Zero { get; } = default;

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

    public static class ValueExtensions
    {
        public static Value Negate(this Value value)
        {
            return (Value)(-(int)value);
        }
    }
    public static class ValueUtills
    {
        public static Value MatedIn(int ply)
        {
            return MateIn(ply).Negate();
        }

        public static Value MateIn(int ply)
        {
            return Value.Mate - ply;
        }

    }
}
