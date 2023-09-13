namespace Chessour
{
    public static class Factory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Piece MakePiece(Color color, PieceType pieceType)
        {
            return (Piece)(((int)color << 3) + (int)pieceType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CastlingRight MakeCastlingRight(Color side, CastlingRight castlingRight)
        {
            return (side == Color.Black ? CastlingRight.BlackSide : CastlingRight.WhiteSide) & castlingRight;
        }
    }
}
