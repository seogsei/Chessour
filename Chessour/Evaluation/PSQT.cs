using Chessour.Evaluation;

namespace Chessour
{
    internal static class PSQT
    {
        private static readonly Score[][][] bonuses = new Score[(int)PieceType.NB][][]
        {
            //NoPiece
            Array.Empty<Score[]>(),

            //Pawn
            new Score [(int)Rank.NB][]
            {
                new Score[(int)File.NB] { new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0) },
                new Score[(int)File.NB] { new(  2, -8), new(  4, -6), new( 11,  9), new( 18,  5), new( 16, 16), new( 21,  6), new(  9, -6), new( -3,-18) },
                new Score[(int)File.NB] { new( -9, -9), new(-15, -7), new( 11,-10), new( 15,  5), new( 31,  2), new( 23,  3), new(  6, -8), new(-20, -5) },
                new Score[(int)File.NB] { new( -3,  7), new(-20,  1), new(  8, -8), new( 19, -2), new( 39,-14), new( 17,-13), new(  2,-11), new( -5, -6) },
                new Score[(int)File.NB] { new( 11, 12), new( -4,  6), new(-11,  2), new(  2, -6), new( 11, -5), new(  0, -4), new(-12, 14), new(  5,  9) },
                new Score[(int)File.NB] { new(  3, 27), new(-11, 18), new( -6, 19), new( 22, 29), new( -8, 30), new( -5,  9), new(-14,  8), new(-11, 14) },
                new Score[(int)File.NB] { new( -7, -1), new(  6,-14), new( -2, 13), new(-11, 22), new(  4, 24), new(-14, 17), new( 10,  7), new( -9,  7) },
                new Score[(int)File.NB] { new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0) },
            },

            //Knight
            new Score [(int)Rank.NB][]
            {
                new Score[(int)File.NB / 2]{ new(-175, -96), new(-92,-65), new(-74,-49), new(-73,-21) },
                new Score[(int)File.NB / 2]{ new( -77, -67), new(-41,-54), new(-27,-18), new(-15,  8) },
                new Score[(int)File.NB / 2]{ new( -61, -40), new(-17,-27), new(  6, -8), new( 12, 29) },
                new Score[(int)File.NB / 2]{ new( -35, -35), new(  8, -2), new( 40, 13), new( 49, 28) },
                new Score[(int)File.NB / 2]{ new( -34, -45), new( 13,-16), new( 44,  9), new( 51, 39) },
                new Score[(int)File.NB / 2]{ new(  -9, -51), new( 22,-44), new( 58,-16), new( 53, 17) },
                new Score[(int)File.NB / 2]{ new( -67, -69), new(-27,-50), new(  4,-51), new( 37, 12) },
                new Score[(int)File.NB / 2]{ new(-201,-100), new(-83,-88), new(-56,-56), new(-26,-17) },
            },

            //Bishop
            new Score [(int)Rank.NB][]
            {
                new Score[(int)File.NB / 2]{ new(-37,-40), new(-4 ,-21), new( -6,-26), new(-16, -8) },
                new Score[(int)File.NB / 2]{ new(-11,-26), new(  6, -9), new( 13,-12), new(  3,  1) },
                new Score[(int)File.NB / 2]{ new(-5 ,-11), new( 15, -1), new( -4, -1), new( 12,  7) },
                new Score[(int)File.NB / 2]{ new(-4 ,-14), new(  8, -4), new( 18,  0), new( 27, 12) },
                new Score[(int)File.NB / 2]{ new(-8 ,-12), new( 20, -1), new( 15,-10), new( 22, 11) },
                new Score[(int)File.NB / 2]{ new(-11,-21), new(  4,  4), new(  1,  3), new(  8,  4) },
                new Score[(int)File.NB / 2]{ new(-12,-22), new(-10,-14), new(  4, -1), new(  0,  1) },
                new Score[(int)File.NB / 2]{ new(-34,-32), new(  1,-29), new(-10,-26), new(-16,-17) },
            },
            
            //Rook
            new Score [(int)Rank.NB][]
            {
                new Score[(int)File.NB / 2]{ new(-31, -9), new(-20,-13), new(-14,-10), new(-5, -9) },
                new Score[(int)File.NB / 2]{ new(-21,-12), new(-13, -9), new( -8, -1), new( 6, -2) },
                new Score[(int)File.NB / 2]{ new(-25,  6), new(-11, -8), new( -1, -2), new( 3, -6) },
                new Score[(int)File.NB / 2]{ new(-13, -6), new( -5,  1), new( -4, -9), new(-6,  7) },
                new Score[(int)File.NB / 2]{ new(-27, -5), new(-15,  8), new( -4,  7), new( 3, -6) },
                new Score[(int)File.NB / 2]{ new(-22,  6), new( -2,  1), new(  6, -7), new(12, 10) },
                new Score[(int)File.NB / 2]{ new( -2,  4), new( 12,  5), new( 16, 20), new(18, -5) },
                new Score[(int)File.NB / 2]{ new(-17, 18), new(-19,  0), new( -1, 19), new( 9, 13) },
            },
            
            //Queen
            new Score [(int)Rank.NB][]
            {
                new Score[(int)File.NB / 2]{ new( 3,-69), new(-5,-57), new(-5,-47), new( 4,-26) },
                new Score[(int)File.NB / 2]{ new(-3,-54), new( 5,-31), new( 8,-22), new(12, -4) },
                new Score[(int)File.NB / 2]{ new(-3,-39), new( 6,-18), new(13, -9), new( 7,  3) },
                new Score[(int)File.NB / 2]{ new( 4,-23), new( 5, -3), new( 9, 13), new( 8, 24) },
                new Score[(int)File.NB / 2]{ new( 0,-29), new(14, -6), new(12,  9), new( 5, 21) },
                new Score[(int)File.NB / 2]{ new(-4,-38), new(10,-18), new( 6,-11), new( 8,  1) },
                new Score[(int)File.NB / 2]{ new(-5,-50), new( 6,-27), new(10,-24), new( 8, -8) },
                new Score[(int)File.NB / 2]{ new(-2,-74), new(-2,-52), new( 1,-43), new(-2,-34) },
            },
            
            //King
            new Score [(int)Rank.NB][]
            {
                new Score[(int)File.NB / 2]{ new(270, 10), new(330, 40), new(270, 80), new(200, 80) },
                new Score[(int)File.NB / 2]{ new(270, 40), new(300,100), new(230,110), new(170,130) },
                new Score[(int)File.NB / 2]{ new(190, 90), new(258,140), new(196,165), new( 12,170) },
                new Score[(int)File.NB / 2]{ new(164,100), new(190,160), new(152,170), new( 50,175) },
                new Score[(int)File.NB / 2]{ new(154,100), new(170,160), new(100,200), new( 48,200) },
                new Score[(int)File.NB / 2]{ new(123, 90), new(142,170), new( 84,200), new( 55,200) },
                new Score[(int)File.NB / 2]{ new( 80, 40), new(120,110), new( 60,110), new( 39,130) },
                new Score[(int)File.NB / 2]{ new( 59, 11), new( 80, 60), new( 40, 75), new(  0, 80) },
            },
        };
        private static readonly Score[,] psqt = new Score[(int)Piece.NB, (int)Square.NB];
        static PSQT()
        {
            Span<Piece> pieces = stackalloc Piece[]
                { Piece.WhitePawn, Piece.WhiteKnight, Piece.WhiteBishop, Piece.WhiteRook, Piece.WhiteQueen, Piece.WhiteKing };

            foreach (Piece pc in pieces)
            {
                Score pieceScore = Pieces.PieceScore(pc);

                for (Square s = Square.a1; s <= Square.h8; s++)
                {
                    psqt[(int)pc, (int)s] = pieceScore + (pc.TypeOf() == PieceType.Pawn ? bonuses[(int)pc][(int)s.GetRank()][(int)s.GetFile()]
                                                                                         : bonuses[(int)pc][(int)s.GetRank()][s.GetFile().EdgeDistance()]); ;

                    psqt[(int)pc.Flip(), (int)s.FlipRank()] = -psqt[(int)pc, (int)s];

                }
            }
        }

        public static Score GetScore(Piece pc, Square s)
        {
            return psqt[(int)pc, (int)s];
        }
    }
}
