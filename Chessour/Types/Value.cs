namespace Chessour.Types
{
    public enum Value
    {
        Zero = 0,
        Draw = 0,
        KnownWin = MateInMaxPly - 1,
        MateInMaxPly = Mate - MAX_PLY,
        Mate = 32000,
        Max = 32001,

        KnownLoss = -KnownWin,
        Mated = -Mate,
        Min = -Max,

        PawnMG = 100, PawnEG = 100,
        KnightMG = 305, KnightEG = 305,
        BishopMG = 333, BishopEG = 333,
        RookMG = 563, RookEG = 563,
        QueenMG = 950, QueenEG = 950,
    }

    public static partial class Core
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value Negate(this Value value)
        {
            return (Value)(-(int)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value MatedIn(int ply)
        {
            return Value.Mated + ply;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value MateIn(int ply)
        {
            return Value.Mate - ply;
        }

        public static Value Max(Value lhs, Value rhs)
        {
            return lhs > rhs ? lhs : rhs;
        }

        public static Value Min(Value lhs, Value rhs)
        {
            return lhs < rhs ? lhs : rhs;
        }
    }
}

