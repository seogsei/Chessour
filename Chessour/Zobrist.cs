namespace Chessour;

internal sealed partial class Position
{
    private static class Zobrist
    {
        private static readonly Key[,] pieceKeys = new Key[(int)Piece.NB, (int)Square.NB];
        private static readonly Key[] castlingKeys = new Key[(int)CastlingRight.NB];
        private static readonly Key[] enPassantKeys = new Key[(int)File.NB];
        private static readonly Key sideKey;

        public static Key SideKey
        {
            get
            {
                return sideKey;
            }
        }

        public static Key PieceKey(Piece piece, Square square)
        {
            return pieceKeys[(int)piece, (int)square];
        }

        public static Key CastlingKey(CastlingRight castlingRights)
        {
            return castlingKeys[(int)castlingRights];
        }

        public static Key EnPassantKey(Square epSquare)
        {
            return enPassantKeys[(int)epSquare.GetFile()];
        }

        static Zobrist()
        {
            Random rand = new();

            for (int p = 0; p < pieceKeys.GetLength(0); p++)
                for (Square s = Square.a1; s <= Square.h8; s++)
                    pieceKeys[p, (int)s] = rand.NextUInt64();

            sideKey = rand.NextUInt64();

            for (CastlingRight cr = CastlingRight.None; cr <= CastlingRight.All; cr++)
                castlingKeys[(int)cr] = rand.NextUInt64();

            for (File f = 0; f <= File.h; f++)
                enPassantKeys[(int)f] = rand.NextUInt64();
        }
    }
}
