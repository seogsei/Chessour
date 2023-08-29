using System.Numerics;

namespace Chessour.Search
{
    public struct MoveScore : IComparisonOperators<MoveScore, MoveScore, bool>, IComparisonOperators<MoveScore, int, bool>
    {
        public MoveScore(Move move)
        {
            Move = move;
            Score = default;
        }

        public MoveScore(Move move, int score)
        {
            Move = move;
            Score = score;
        }

        public readonly Move Move { get; }
        public int Score { get; set; }

        public static implicit operator MoveScore(Move move) => new(move);
        public static implicit operator Move(MoveScore moveScore) => moveScore.Move;

        public static bool operator >(MoveScore left, MoveScore right) => left.Score > right.Score;
        public static bool operator >=(MoveScore left, MoveScore right) => left.Score >= right.Score;
        public static bool operator <(MoveScore left, MoveScore right) => left.Score < right.Score;
        public static bool operator <=(MoveScore left, MoveScore right) => left.Score <= right.Score;

        static bool IEqualityOperators<MoveScore, MoveScore, bool>.operator ==(MoveScore left, MoveScore right) => left.Score == right.Score;
        static bool IEqualityOperators<MoveScore, MoveScore, bool>.operator !=(MoveScore left, MoveScore right) => left.Score != right.Score;

        public static bool operator >(MoveScore left, int right) => left.Score > right;

        public static bool operator >=(MoveScore left, int right) => left.Score >= right;

        public static bool operator <(MoveScore left, int right) => left.Score < right;

        public static bool operator <=(MoveScore left, int right) => left.Score <= right;

        static bool IEqualityOperators<MoveScore, int, bool>.operator ==(MoveScore left, int right) => left.Score == right;
        static bool IEqualityOperators<MoveScore, int, bool>.operator !=(MoveScore left, int right) => left.Score != right;
    }
}
