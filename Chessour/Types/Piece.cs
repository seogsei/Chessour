namespace Chessour.Types
{
    public enum PieceType
    {
        None = 0,
        Pawn, Knight, Bishop, Rook, Queen, King,
        NB,
        AllPieces = 0,
    }

    public enum Piece
    {
        None,
        WhitePawn = PieceType.Pawn, WhiteKnight, WhiteBishop, WhiteRook, WhiteQueen, WhiteKing,
        BlackPawn = PieceType.Pawn + 8, BlackKnight, BlackBishop, BlackRook, BlackQueen, BlackKing,
        NB
    }


    public static partial class Core
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Piece MakePiece(Color color, PieceType pieceType)
        {
            return (Piece)(((int)color << 3) + (int)pieceType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PieceType GetPieceType(this Piece piece)
        {
            return (PieceType)((int)piece & 7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color GetColor(this Piece piece)
        {
            return (Color)((int)piece >> 3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Piece Flip(this Piece piece)
        {
            return piece ^ (Piece)8;
        }

    }
}