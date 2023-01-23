namespace Chessour.Types
{
    public enum CastlingRight
    {
        None = 0,
        WhiteKingSide = 1,
        WhiteQueenSide = 2,
        BlackKingSide = 4,
        BlackQueenSide = 8,

        WhiteSide = WhiteKingSide | WhiteQueenSide,
        BlackSide = BlackKingSide | BlackQueenSide,

        KingSide = WhiteKingSide | BlackKingSide,
        QueenSide = WhiteQueenSide | BlackQueenSide,

        All = WhiteSide | BlackSide,
        NB
    }

    public static partial class Core
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CastlingRight MakeCastlingRight(Color side, CastlingRight cr)
        {
            return (side == Color.White ? CastlingRight.WhiteSide : CastlingRight.BlackSide) & cr;
        }
    }
}
