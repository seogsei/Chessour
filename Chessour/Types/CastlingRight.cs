using System;

namespace Chessour.Types
{

    [Flags]
    public enum CastlingRight : byte
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

    public static partial class Factory
    {
        public static CastlingRight MakeCastlingRight(Color us, CastlingRight cr)
        {
            return cr & (CastlingRight)((int)CastlingRight.WhiteSide << (2 * (int)us));
        }
    }
}
