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

        public Move Move { get; }
        public short Score { get; set; }

        public static implicit operator MoveScore(Move m)
        {
            return new(m);
        }

        public static implicit operator Move(MoveScore m)
        {
            return m.Move;
        }

        public static bool operator <(MoveScore lhs, MoveScore rhs)
        {
            return lhs.Score < rhs.Score;
        }

        public static bool operator >(MoveScore lhs, MoveScore rhs)
        {
            return lhs.Score > rhs.Score;
        }
    }
}
