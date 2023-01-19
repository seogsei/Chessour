namespace Chessour
{
    public partial class Position
    {
        public sealed class StateInfo
        {
            public StateInfo? Previous { get; set; }
            public Move LastMove { get; set; }
            public Piece CapturedPiece { get; set; }
            public CastlingRight CastlingRights { get; set; }
            public Square EnPassantSquare { get; set; }
            public int FiftyMoveCounter { get; set; }
            public Key ZobristKey { get; set; }
            public Bitboard Checkers { get; set; }
            public Bitboard[] BlockersForKing { get; } = new Bitboard[(int)Color.NB];
            public Bitboard[] Pinners { get; } = new Bitboard[(int)Color.NB];
            public Bitboard[] CheckSquares { get; } = new Bitboard[(int)PieceType.NB];
        }
    }
}
