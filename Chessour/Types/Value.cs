using System.Reflection.Metadata;

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

        PawnMG = 100, PawnEG = 110,
        KnightMG = 300, KnightEG = 300,
        BishopMG = 300, BishopEG = 300,
        RookMG = 440, RookEG = 480,
        QueenMG = 890, QueenEG = 950,
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

