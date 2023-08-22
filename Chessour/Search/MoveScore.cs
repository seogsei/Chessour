using System.Numerics;
using System.Runtime.InteropServices;

namespace Chessour.Search
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MoveScore
    {
        public MoveScore(Move move)
        {
            Move = move;
            Score = 0;
        }

        public MoveScore(Move move, short score)
        {
            Move = move;
            Score = score;
        }

        public readonly Move Move { get; }
        public int Score { get; set; }

        public static bool operator <(MoveScore left, MoveScore right) => left.Score < right.Score;
        public static bool operator >(MoveScore left, MoveScore right) => left.Score > right.Score;
        public static bool operator <=(MoveScore left, MoveScore right) => left.Score <= right.Score;
        public static bool operator >=(MoveScore left, MoveScore right) => left.Score >= right.Score;

        public static implicit operator MoveScore(Move move) => new(move);
        public static implicit operator Move(MoveScore moveScore) => moveScore.Move;
    }
}
