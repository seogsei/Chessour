global using Value = System.Int32;

using Chessour.Search;
using System.Text;

namespace Chessour.Evaluation
{
    public static class ValueConstants
    {
        public const Value Value_Draw = 0;
        public const Value KnownWin = 10000;
        public const Value Value_Mate = 32000;
        public const Value Value_INF = 32001;

        public const Value Value_MateInMaxPly = Value_Mate - DepthConstants.MAX_PLY;
        public const Value Value_MatedInMaxPly = -Value_MateInMaxPly;

        public const Value PawnMGValue = 100;
        public const Value PawnEGValue = 100;

        public const Value KnightMGValue = 305;
        public const Value KnightEGValue = 305;

        public const Value BishopMGValue = 333;
        public const Value BishopEGValue = 333;

        public const Value RookMGValue = 563;
        public const Value RookEGValue = 563;

        public const Value QueenMGValue = 950;
        public const Value QueenEGValue = 950;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value MatedIn(int ply)
        {
            return -Value_Mate + ply;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value MateIn(int ply)
        {
            return Value_Mate - ply;
        }

        public static string ToUCIString(this Value value)
        {
            StringBuilder sb = new();

            if (Math.Abs(value) < Value_MateInMaxPly)
            {
                sb.Append("cp ");
                sb.Append(value * 100 / PawnMGValue);
            }
            else
            {
                sb.Append("mate ");
                sb.Append((value > 0 ? Value_Mate - value + 1 : -Value_Mate - value) / 2);
            }

            return sb.ToString();
        }
    }
}
