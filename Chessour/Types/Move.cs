using System.Runtime.CompilerServices;

namespace Chessour.Types
{
    public enum MoveType
    {
        Quiet,
        Promotion = 1 << 14,
        EnPassant = 2 << 14,
        Castling = 3 << 14
    }
    public enum Move
    {
        None,
        Null = 65
    }

    public static class MoveExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square FromSquare(this Move move)
        {
            return (Square)(((int)move >> 6) & 0b111111);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square ToSquare(this Move move)
        {
            return (Square)((int)move & 0b111111);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MoveType TypeOf(this Move move)
        {
            return (MoveType)((int)move & (3 << 14));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PieceType PromotionPiece(this Move move)
        {
            return (PieceType)(((int)move >> 12) & 3) + 2;
        }
    }

    public static partial class Factory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Move MakeMove(Square from, Square to)
        {
            return (Move)(((int)from << 6) | (int)to);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Move MakeCastlingMove(Square kingSquare, Square rookSquare)
        {
            return (Move)((int)MoveType.Castling | ((int)kingSquare << 6) | (int)rookSquare);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Move MakeEnPassantMove(Square from, Square enPassantSquare)
        {
            return (Move)((int)MoveType.EnPassant | ((int)from << 6) | (int)enPassantSquare);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Move MakePromotionMove(Square from, Square to, PieceType promotionPiece)
        {
            return (Move)((int)MoveType.Promotion | (((int)promotionPiece - 2) << 12) | ((int)from << 6) | (int)to);
        }
    }
}
