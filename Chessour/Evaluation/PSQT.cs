namespace Chessour.Evaluation
{
    internal static class PSQT
    {
        static PSQT()
        {
            Span<Piece> whitePieces = stackalloc Piece[]
            {
                Piece.WhitePawn,
                Piece.WhiteKnight,
                Piece.WhiteBishop,
                Piece.WhiteRook,
                Piece.WhiteQueen,
                Piece.WhiteKing
            };

            foreach (Piece whitePiece in whitePieces)
            {
                ScoreTuple pieceScore = Pieces.PieceScore(whitePiece);
                Piece blackPiece = whitePiece.FlipColor();

                psqt[(int)whitePiece] = new ScoreTuple[(int)Square.NB];
                psqt[(int)blackPiece] = new ScoreTuple[(int)Square.NB];

                for (Square square = Square.a1; square < Square.NB; square++)
                {
                    psqt[(int)whitePiece][(int)square] = pieceScore + (whitePiece.TypeOf() == PieceType.Pawn ? bonuses[(int)whitePiece][(int)square.GetRank(), (int)square.GetFile()]
                                                                                                        : bonuses[(int)whitePiece][(int)square.GetRank(), square.GetFile().EdgeDistance()]); ;

                    psqt[(int)blackPiece][(int)square.FlipRank()] = -psqt[(int)whitePiece][(int)square];
                }
            }
        }

        private static readonly ScoreTuple[][] psqt = new ScoreTuple[(int)Piece.NB][];

        private static readonly ScoreTuple[,] pawnBonuses = new ScoreTuple[(int)Rank.NB, (int)File.NB]
        {
                { new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0) },
                { new(  5,  0), new( 10,  0), new( 10,  0), new(-20,  0), new(-20,  0), new( 10,  0), new( 10,  0), new(  5,  0) },
                { new(  5,  0), new( -5,  0), new(-10,  0), new(  0,  0), new(  0,  0), new(-10,  0), new( -5,  0), new(  5,  0) },
                { new(  0,  0), new(  0,  0), new(  0,  0), new( 20,  0), new( 20,  0), new(  0,  0), new(  0,  0), new(  0,  0) },
                { new(  5,  0), new(  5,  0), new( 10,  0), new( 25,  0), new( 25,  0), new( 10,  0), new(  5,  0), new(  5,  0) },
                { new( 10,  0), new( 10,  0), new( 20,  0), new( 30,  0), new( 30,  0), new( 20,  0), new( 10,  0), new( 10,  0) },
                { new( 50,  0), new( 50,  0), new( 50,  0), new( 50,  0), new( 50,  0), new( 50,  0), new( 50,  0), new( 50,  0) },
                { new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0) },
        };

        private static readonly ScoreTuple[,] knightBonuses = new ScoreTuple[(int)Rank.NB, (int)File.NB / 2]
        {
                { new(-175, -96), new(-92,-65), new(-74,-49), new(-73,-21) },
                { new( -77, -67), new(-41,-54), new(-27,-18), new(-15,  8) },
                { new( -61, -40), new(-17,-27), new(  6, -8), new( 12, 29) },
                { new( -35, -35), new(  8, -2), new( 40, 13), new( 49, 28) },
                { new( -34, -45), new( 13,-16), new( 44,  9), new( 51, 39) },
                { new(  -9, -51), new( 22,-44), new( 58,-16), new( 53, 17) },
                { new( -67, -69), new(-27,-50), new(  4,-51), new( 37, 12) },
                { new(-201,-100), new(-83,-88), new(-56,-56), new(-26,-17) },
        };

        private static readonly ScoreTuple[,] bishopBonuses = new ScoreTuple[(int)Rank.NB, (int)File.NB / 2]
        {
                { new(-37,-40), new(-4 ,-21), new( -6,-26), new(-16, -8) },
                { new(-11,-26), new(  6, -9), new( 13,-12), new(  3,  1) },
                { new(-5 ,-11), new( 15, -1), new( -4, -1), new( 12,  7) },
                { new(-4 ,-14), new(  8, -4), new( 18,  0), new( 27, 12) },
                { new(-8 ,-12), new( 20, -1), new( 15,-10), new( 22, 11) },
                { new(-11,-21), new(  4,  4), new(  1,  3), new(  8,  4) },
                { new(-12,-22), new(-10,-14), new(  4, -1), new(  0,  1) },
                { new(-34,-32), new(  1,-29), new(-10,-26), new(-16,-17) },
        };

        private static readonly ScoreTuple[,] rookBonuses = new ScoreTuple[(int)Rank.NB, (int)File.NB / 2]
        {
                { new(-31, -9), new(-20,-13), new(-14,-10), new(-5, -9) },
                { new(-21,-12), new(-13, -9), new( -8, -1), new( 6, -2) },
                { new(-25,  6), new(-11, -8), new( -1, -2), new( 3, -6) },
                { new(-13, -6), new( -5,  1), new( -4, -9), new(-6,  7) },
                { new(-27, -5), new(-15,  8), new( -4,  7), new( 3, -6) },
                { new(-22,  6), new( -2,  1), new(  6, -7), new(12, 10) },
                { new( -2,  4), new( 12,  5), new( 16, 20), new(18, -5) },
                { new(-17, 18), new(-19,  0), new( -1, 19), new( 9, 13) },
        };

        private static readonly ScoreTuple[,] queenBonuses = new ScoreTuple[(int)Rank.NB, (int)File.NB / 2]
        {
                { new( 3,-69), new(-5,-57), new(-5,-47), new( 4,-26) },
                { new(-3,-54), new( 5,-31), new( 8,-22), new(12, -4) },
                { new(-3,-39), new( 6,-18), new(13, -9), new( 7,  3) },
                { new( 4,-23), new( 5, -3), new( 9, 13), new( 8, 24) },
                { new( 0,-29), new(14, -6), new(12,  9), new( 5, 21) },
                { new(-4,-38), new(10,-18), new( 6,-11), new( 8,  1) },
                { new(-5,-50), new( 6,-27), new(10,-24), new( 8, -8) },
                { new(-2,-74), new(-2,-52), new( 1,-43), new(-2,-34) },
        };

        private static readonly ScoreTuple[,] kingBonuses = new ScoreTuple[(int)Rank.NB, (int)File.NB / 2]
        {
                { new(270, 10), new(330, 40), new(270, 80), new(200, 80) },
                { new(270, 40), new(300,100), new(230,110), new(170,130) },
                { new(190, 90), new(258,140), new(196,165), new( 12,170) },
                { new(164,100), new(190,160), new(152,170), new( 50,175) },
                { new(154,100), new(170,160), new(100,200), new( 48,200) },
                { new(123, 90), new(142,170), new( 84,200), new( 55,200) },
                { new( 80, 40), new(120,110), new( 60,110), new( 39,130) },
                { new( 59, 11), new( 80, 60), new( 40, 75), new(  0, 80) },
        };

        private static readonly ScoreTuple[][,] bonuses = new ScoreTuple[(int)PieceType.NB][,]
        {
                new ScoreTuple[0,0],
                pawnBonuses,
                knightBonuses,
                bishopBonuses,
                rookBonuses,
                queenBonuses,
                kingBonuses
        };

        public static ScoreTuple Get(Piece piece, Square square)
        {
            return psqt[(int)piece][(int)square];
        }
    }
}
