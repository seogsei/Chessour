using System.Text;

namespace Chessour
{
    internal static class FEN
    {
        public const string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        public const string PieceValueToChar = " PNBRQK  pnbrqk";
        public static string Generate(IPositionInfo positionInfo)
        {
            StringBuilder sb = new();


            for (Rank r = Rank.R8; r >= Rank.R1; r--)
            {
                for (File f = File.a; f <= File.h; f++)
                {
                    int emptyCounter;
                    for (emptyCounter = 0; f <= File.h && positionInfo.PieceAt(MakeSquare(f, r)) == Piece.None; f++)
                        emptyCounter++;
                    if (emptyCounter > 0)
                        sb.Append(emptyCounter);

                    if (f <= File.h)
                        sb.Append(PieceValueToChar[(int)positionInfo.PieceAt(MakeSquare(f, r))]);
                }
                if (r > Rank.R1)
                    sb.Append('/');
            }

            sb.Append(positionInfo.ActiveColor == Color.White ? " w " : " b ");

            if (positionInfo.CanCastle(CastlingRight.WhiteKingSide))
                sb.Append('K');
            if (positionInfo.CanCastle(CastlingRight.WhiteQueenSide))
                sb.Append('Q');
            if (positionInfo.CanCastle(CastlingRight.BlackKingSide))
                sb.Append('k');
            if (positionInfo.CanCastle(CastlingRight.BlackQueenSide))
                sb.Append('q');
            if (!positionInfo.CanCastle(CastlingRight.All))
                sb.Append('-');

            sb.Append(' ');
            sb.Append(positionInfo.EnPassantSquare == Square.None ? "-" : positionInfo.EnPassantSquare);
            sb.Append(' ');
            sb.Append(positionInfo.FiftyMoveCounter);
            sb.Append(' ');
            sb.Append(positionInfo.FullMoveCounter);

            return sb.ToString();
        }
    }
}
