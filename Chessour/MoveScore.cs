namespace Chessour
{
    struct MoveScore
    {
        public MoveScore(Move move)
        {
            this.Move = move;
            Score = 0;
        }

        public MoveScore(Move move, int score)
        {
            this.Move = move;
            this.Score = score;
        }

        public Move Move { get; init; }
        public int Score { get; set; }

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
