namespace Chessour
{
    [Flags]
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
}
