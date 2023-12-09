using System.Numerics;

namespace Chessour.Search
{
    public struct MoveScore (Move move, int score = 0): 
        IComparisonOperators<MoveScore, MoveScore, bool>,
        IComparisonOperators<MoveScore, int, bool>
    {
        public Move Move { get; set; } = move;
        public int Score { get; set; } = score;
 
        public static implicit operator MoveScore(Move move)
        {
            return new(move);
        }

        public static implicit operator Move(MoveScore moveScore)
        {
            return moveScore.Move;
        }

        public static bool operator >(MoveScore left, MoveScore right)
        {
            return left.Score > right.Score;
        }

        public static bool operator >=(MoveScore left, MoveScore right)
        {
            return left.Score >= right.Score;
        }

        public static bool operator <(MoveScore left, MoveScore right)
        {
            return left.Score < right.Score;
        }

        public static bool operator <=(MoveScore left, MoveScore right)
        {
            return left.Score <= right.Score;
        }

        static bool IEqualityOperators<MoveScore, MoveScore, bool>.operator ==(MoveScore left, MoveScore right)
        {
            return left.Score == right.Score;
        }

        static bool IEqualityOperators<MoveScore, MoveScore, bool>.operator !=(MoveScore left, MoveScore right)
        {
            return left.Score != right.Score;
        }

        public static bool operator >(MoveScore left, int right)
        {
            return left.Score > right;
        }

        public static bool operator >=(MoveScore left, int right)
        {
            return left.Score >= right;
        }

        public static bool operator <(MoveScore left, int right)
        {
            return left.Score < right;
        }

        public static bool operator <=(MoveScore left, int right)
        {
            return left.Score <= right;
        }

        static bool IEqualityOperators<MoveScore, int, bool>.operator ==(MoveScore left, int right)
        {
            return left.Score == right;
        }

        static bool IEqualityOperators<MoveScore, int, bool>.operator !=(MoveScore left, int right)
        {
            return left.Score != right;
        }
    }
}
