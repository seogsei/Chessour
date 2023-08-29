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
        public static Square MakeSquare(File file, Rank rank)
        {
            return (Square)(((int)rank << 3) + (int)file);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CastlingRight MakeCastlingRight(Color side, CastlingRight cr)
        {
            return (side == Color.White ? CastlingRight.WhiteSide : CastlingRight.BlackSide) & cr;
        }
    }
}
