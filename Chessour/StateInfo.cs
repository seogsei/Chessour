namespace Chessour;

internal sealed partial class Position
{
    public sealed class StateInfo
    {
        public StateInfo? Previous { get; set; }

        public Key ZobristKey { get; set; }
        public CastlingRight CastlingRights { get; set; }
        public Square EnPassantSquare { get; set; }
        public int HalfMoveClock { get; set; }

        public int PliesFromNull { get; set; }
        public int Repetition { get; set; }
        public Piece Captured { get; set; }

        public Bitboard Checkers { get; set; }
        public Bitboard[] BlockersForKing { get; } = new Bitboard[(int)Color.NB];
        public Bitboard[] Pinners { get; } = new Bitboard[(int)Color.NB];
        public Bitboard[] CheckSquares { get; } = new Bitboard[(int)PieceType.NB];

        internal void Clear()
        {
            ZobristKey = default;
            CastlingRights = default;
            EnPassantSquare = default;
            HalfMoveClock = default;
            PliesFromNull = default;
            Previous = default;
            Captured = default;
            Repetition = default;
            Checkers = default;

            Array.Clear(BlockersForKing);
            Array.Clear(Pinners);
            Array.Clear(CheckSquares);
        }
    }
}
