namespace Chessour
{
    public struct MoveScore
    {
        public Move Move { get; }
        public int Score { get; set; }

        public MoveScore(Move move)
        {
            Move = move;
            Score = 0;
        }

        public MoveScore(Move move, int score)
        {
            Move = move;
            Score = score;
        }

        public static implicit operator MoveScore(Move m) => new(m);
        public static implicit operator Move(MoveScore m) => m.Move;

        public static bool operator <(MoveScore lhs, MoveScore rhs) => lhs.Score < rhs.Score;
        public static bool operator >(MoveScore lhs, MoveScore rhs) => lhs.Score > rhs.Score;
    }
}
