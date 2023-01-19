using Chessour.Types;
using System;

namespace Chessour
{
    internal static class Zobrist
    {
        static readonly Key[,] pieceKeys = new Key[(int)Piece.NB, (int)Square.NB];
        static readonly Key[] castlingKeys = new Key[(int)CastlingRight.NB];
        static readonly Key[] enPassantKeys = new Key[(int)File.NB];
        static readonly Key sideKey;
        public static Key PieceKey(Piece p, Square sq) => pieceKeys[(int)p, (int)sq];
        public static Key SideKey { get => sideKey; }
        public static Key CastlingKey(CastlingRight cr) => castlingKeys[(int)cr];
        public static Key EnPassantKey(Square epSqr) => enPassantKeys[(int)epSqr.FileOf()];

        public static void Init() { }
        static Zobrist()
        {
            Random rand = new();

            for (int p = 0; p < pieceKeys.GetLength(0); p++)
                for (Square s = Square.a1; s <= Square.h8; s++)
                    pieceKeys[p, (int)s] = (Key)rand.NextUInt64();

            sideKey = (Key)rand.NextUInt64();

            for (CastlingRight cr = CastlingRight.None; cr <= CastlingRight.All; cr++)
                castlingKeys[(int)cr] = (Key)rand.NextUInt64();

            for (File f = 0; f <= File.h; f++)
                enPassantKeys[(int)f] = (Key)rand.NextUInt64();
        }
    }
}
