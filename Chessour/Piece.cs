namespace Chessour
{
    public enum Color
    {
        White, Black, NB
    }

    public enum PieceType
    {
        None = 0,
        AllPieces = 0,
        Pawn, Knight, Bishop, Rook, Queen, King,
        NB = 7,
    }

    public enum Piece
    {
        None,
        WhitePawn = PieceType.Pawn, WhiteKnight, WhiteBishop, WhiteRook, WhiteQueen, WhiteKing,
        BlackPawn = PieceType.Pawn + 8, BlackKnight, BlackBishop, BlackRook, BlackQueen, BlackKing,
        NB = 16
    }

    internal static class PieceConstants
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ColorOf(this Piece piece)
        {
            return (Color)((int)piece >> 3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Flip(this Color color)
        {
            return color ^ Color.Black;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Piece Flip(this Piece piece)
        {
            return piece ^ (Piece)8;
        }
        public static bool IsValid(this Color color)
        {
            return color == Color.White || color == Color.Black;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Piece MakePiece(Color color, PieceType pieceType)
        {
            return (Piece)(((int)color << 3) + (int)pieceType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Direction PawnPush(Color side)
        {
            return side == Color.White ? Direction.North : Direction.South;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PieceType TypeOf(this Piece piece)
        {
            return (PieceType)((int)piece & 7);
        }
    }
}
