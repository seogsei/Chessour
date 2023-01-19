using Chessour.Types;
using System;

namespace Chessour
{
    static class PSQT
    {

        static readonly Score[][][] bonuses = new Score[(int)PieceType.NB][][]
        {
            //NoPiece
            Array.Empty<Score[]>(),

            //Pawn
            new Score [(int)Rank.NB][]
            {
                new Score[(int)File.NB] { S(  0,  0), S(  0,  0), S(  0,  0), S(  0,  0), S(  0,  0), S(  0,  0), S(  0,  0), S(  0,  0) },
                new Score[(int)File.NB] { S(  2, -8), S(  4, -6), S( 11,  9), S( 18,  5), S( 16, 16), S( 21,  6), S(  9, -6), S( -3,-18) },
                new Score[(int)File.NB] { S( -9, -9), S(-15, -7), S( 11,-10), S( 15,  5), S( 31,  2), S( 23,  3), S(  6, -8), S(-20, -5) },
                new Score[(int)File.NB] { S( -3,  7), S(-20,  1), S(  8, -8), S( 19, -2), S( 39,-14), S( 17,-13), S(  2,-11), S( -5, -6) },
                new Score[(int)File.NB] { S( 11, 12), S( -4,  6), S(-11,  2), S(  2, -6), S( 11, -5), S(  0, -4), S(-12, 14), S(  5,  9) },
                new Score[(int)File.NB] { S(  3, 27), S(-11, 18), S( -6, 19), S( 22, 29), S( -8, 30), S( -5,  9), S(-14,  8), S(-11, 14) },
                new Score[(int)File.NB] { S( -7, -1), S(  6,-14), S( -2, 13), S(-11, 22), S(  4, 24), S(-14, 17), S( 10,  7), S( -9,  7) },
                new Score[(int)File.NB] { S(  0,  0), S(  0,  0), S(  0,  0), S(  0,  0), S(  0,  0), S(  0,  0), S(  0,  0), S(  0,  0) },
            },

            //Knight
            new Score [(int)Rank.NB][]
            {
                new Score[(int)File.NB / 2]{ S(-175, -96), S(-92,-65), S(-74,-49), S(-73,-21) },
                new Score[(int)File.NB / 2]{ S( -77, -67), S(-41,-54), S(-27,-18), S(-15,  8) },
                new Score[(int)File.NB / 2]{ S( -61, -40), S(-17,-27), S(  6, -8), S( 12, 29) },
                new Score[(int)File.NB / 2]{ S( -35, -35), S(  8, -2), S( 40, 13), S( 49, 28) },
                new Score[(int)File.NB / 2]{ S( -34, -45), S( 13,-16), S( 44,  9), S( 51, 39) },
                new Score[(int)File.NB / 2]{ S(  -9, -51), S( 22,-44), S( 58,-16), S( 53, 17) },
                new Score[(int)File.NB / 2]{ S( -67, -69), S(-27,-50), S(  4,-51), S( 37, 12) },
                new Score[(int)File.NB / 2]{ S(-201,-100), S(-83,-88), S(-56,-56), S(-26,-17) },
            },

            //Bishop
            new Score [(int)Rank.NB][]
            {
                new Score[(int)File.NB / 2]{ S(-37,-40), S(-4 ,-21), S( -6,-26), S(-16, -8) },
                new Score[(int)File.NB / 2]{ S(-11,-26), S(  6, -9), S( 13,-12), S(  3,  1) },
                new Score[(int)File.NB / 2]{ S(-5 ,-11), S( 15, -1), S( -4, -1), S( 12,  7) },
                new Score[(int)File.NB / 2]{ S(-4 ,-14), S(  8, -4), S( 18,  0), S( 27, 12) },
                new Score[(int)File.NB / 2]{ S(-8 ,-12), S( 20, -1), S( 15,-10), S( 22, 11) },
                new Score[(int)File.NB / 2]{ S(-11,-21), S(  4,  4), S(  1,  3), S(  8,  4) },
                new Score[(int)File.NB / 2]{ S(-12,-22), S(-10,-14), S(  4, -1), S(  0,  1) },
                new Score[(int)File.NB / 2]{ S(-34,-32), S(  1,-29), S(-10,-26), S(-16,-17) },
            },
            
            //Rook
            new Score [(int)Rank.NB][]
            {
                new Score[(int)File.NB / 2]{ S(-31, -9), S(-20,-13), S(-14,-10), S(-5, -9) },
                new Score[(int)File.NB / 2]{ S(-21,-12), S(-13, -9), S( -8, -1), S( 6, -2) },
                new Score[(int)File.NB / 2]{ S(-25,  6), S(-11, -8), S( -1, -2), S( 3, -6) },
                new Score[(int)File.NB / 2]{ S(-13, -6), S( -5,  1), S( -4, -9), S(-6,  7) },
                new Score[(int)File.NB / 2]{ S(-27, -5), S(-15,  8), S( -4,  7), S( 3, -6) },
                new Score[(int)File.NB / 2]{ S(-22,  6), S( -2,  1), S(  6, -7), S(12, 10) },
                new Score[(int)File.NB / 2]{ S( -2,  4), S( 12,  5), S( 16, 20), S(18, -5) },
                new Score[(int)File.NB / 2]{ S(-17, 18), S(-19,  0), S( -1, 19), S( 9, 13) },
            },
            
            //Queen
            new Score [(int)Rank.NB][]
            {
                new Score[(int)File.NB / 2]{ S( 3,-69), S(-5,-57), S(-5,-47), S( 4,-26) },
                new Score[(int)File.NB / 2]{ S(-3,-54), S( 5,-31), S( 8,-22), S(12, -4) },
                new Score[(int)File.NB / 2]{ S(-3,-39), S( 6,-18), S(13, -9), S( 7,  3) },
                new Score[(int)File.NB / 2]{ S( 4,-23), S( 5, -3), S( 9, 13), S( 8, 24) },
                new Score[(int)File.NB / 2]{ S( 0,-29), S(14, -6), S(12,  9), S( 5, 21) },
                new Score[(int)File.NB / 2]{ S(-4,-38), S(10,-18), S( 6,-11), S( 8,  1) },
                new Score[(int)File.NB / 2]{ S(-5,-50), S( 6,-27), S(10,-24), S( 8, -8) },
                new Score[(int)File.NB / 2]{ S(-2,-74), S(-2,-52), S( 1,-43), S(-2,-34) },
            },
            
            //King
            new Score [(int)Rank.NB][]
            {
                new Score[(int)File.NB / 2]{ S(270, 10), S(330, 40), S(270, 80), S(200, 80) },
                new Score[(int)File.NB / 2]{ S(270, 40), S(300,100), S(230,110), S(170,130) },
                new Score[(int)File.NB / 2]{ S(190, 90), S(258,140), S(196,165), S( 12,170) },
                new Score[(int)File.NB / 2]{ S(164,100), S(190,160), S(152,170), S( 50,175) },
                new Score[(int)File.NB / 2]{ S(154,100), S(170,160), S(100,200), S( 48,200) },
                new Score[(int)File.NB / 2]{ S(123, 90), S(142,170), S( 84,200), S( 55,200) },
                new Score[(int)File.NB / 2]{ S( 80, 40), S(120,110), S( 60,110), S( 39,130) },
                new Score[(int)File.NB / 2]{ S( 59, 11), S( 80, 60), S( 40, 75), S(  0, 80) },
            },
        };

        static readonly Score[,] psqt = new Score[(int)Piece.NB, (int)Square.NB];

        public static Score Get(Piece pc, Square s)
        {
            return psqt[(int)pc, (int)s];
        }

        static Score S(int mg, int eg)
        {
            return new(mg, eg);
        }

        public static void Init() { }

        static PSQT()
        {
            Span<Piece> pieces = stackalloc Piece[] { Piece.WhitePawn, Piece.WhiteKnight, Piece.WhiteBishop, Piece.WhiteRook, Piece.WhiteQueen, Piece.WhiteKing };

            foreach (Piece pc in pieces)
            {
                Score pieceScore = Evaluation.PieceValue(pc);

                for (Square s = Square.a1; s <= Square.h8; s++)
                {
                    psqt[(int)pc, (int)s] = pieceScore + (pc.TypeOf() == PieceType.Pawn ? bonuses[(int)pc][(int)s.RankOf()][(int)s.FileOf()]
                                                                                                     : bonuses[(int)pc][(int)s.RankOf()][s.FileOf().EdgeDistance()]);

                    psqt[(int)pc.Opposite(), (int)s.FlipRank()] = -psqt[(int)pc, (int)s];

                }
            }
        }
    }
}
