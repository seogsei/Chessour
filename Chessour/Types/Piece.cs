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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PieceType TypeOf(this Piece piece) => (PieceType)((int)piece & 7);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ColorOf(this Piece piece) => (Color)((int)piece >> 3);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Piece Opposite(this Piece piece) => (Piece)((int)piece ^ 8);
    }

    public static partial class Factory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Piece MakePiece(Color color, PieceType pieceType) => (Piece)(((int)color << 3) | (int)pieceType);
    }
}
