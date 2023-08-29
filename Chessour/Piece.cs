namespace Chessour
{
    public enum Piece
    {
        None,
        WhitePawn = PieceType.Pawn, WhiteKnight, WhiteBishop, WhiteRook, WhiteQueen, WhiteKing,
        BlackPawn = PieceType.Pawn + 8, BlackKnight, BlackBishop, BlackRook, BlackQueen, BlackKing,
        NB = 16
    }

    public enum PieceType
    {
        None = 0,
        AllPieces = 0,
        Pawn, Knight, Bishop, Rook, Queen, King,
        NB = 7,
    }


    public static class PieceExtensions
    {
        private static readonly string pieceToChar = " PNBRQK  pnbrqk";

        public static char PieceToChar(this Piece piece)
        {
            return pieceToChar[(int)piece];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ColorOf(this Piece piece)
        {
            return (Color)((int)piece >> 3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PieceType TypeOf(this Piece piece)
        {
            return (PieceType)((int)piece & 7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Piece FlipColor(this Piece piece)
        {
            return piece ^ (Piece)8;
        }
    }
}
