namespace Chessour.Types
{
    public enum PieceType
    {
        AllPieces = 0,
        None = 0,
        Pawn, Knight, Bishop, Rook, Queen, King,
        NB
    }

    public enum Piece
    {
        None,
        WhitePawn = PieceType.Pawn, WhiteKnight, WhiteBishop, WhiteRook, WhiteQueen, WhiteKing,
        BlackPawn = PieceType.Pawn + 8, BlackKnight, BlackBishop, BlackRook, BlackQueen, BlackKing,
        NB
    }


    public static partial class CoreFunctions
    {
        public static Piece MakePiece(Color color, PieceType pieceType)
        {
            return (Piece)(((int)color << 3) | (int)pieceType);
        }

        public static PieceType GetPieceType(this Piece piece)
        {
            return (PieceType)((int)piece & 7);
        }

        public static Color GetColor(this Piece piece)
        {
            return (Color)((int)piece >> 3);
        }

        public static bool IsValid(this PieceType pieceType)
        {
            return pieceType >= 0 && pieceType < PieceType.NB;
        }

        public static Piece Opposite(this Piece piece)
        {
            return (Piece)((int)piece ^ 8);
        }

    }
}