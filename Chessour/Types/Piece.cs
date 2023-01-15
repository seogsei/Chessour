using System.Runtime.CompilerServices;

namespace Chessour.Types
{
    public enum PieceType
    {
        None,
        Pawn,
        Knight,
        Bishop,
        Rook,
        Queen,
        King,
        NB
    }
    public enum Piece
    {
        None,
        WhitePawn,
        WhiteKnight,
        WhiteBishop,
        WhiteRook,
        WhiteQueen,
        WhiteKing,

        BlackPawn = WhitePawn + 8,
        BlackKnight,
        BlackBishop,
        BlackRook,
        BlackQueen,
        BlackKing,
        NB
    }

    public static class PieceExtensions
    {
        public static PieceType TypeOf(this Piece piece)
        {
            return (PieceType)((int)piece & 7);
        }

        public static Color ColorOf(this Piece piece)
        {
            return (Color)((int)piece >> 3);
        }

        public static Piece Opposite(this Piece piece)
        {
            return (Piece)((int)piece ^ 8);
        }
    }

    public static partial class Factory
    {
        public static Piece MakePiece(Color color, PieceType pieceType)
        {
            return (Piece)(((int)color << 3) | (int)pieceType);
        }
    }
}
