namespace Chessour.Evaluation
{
    internal static class Pieces
    {
        public const int PawnMidGame = 100;
        public const int PawnEndGame = 100;
        public const int KnightMidGame = 305;
        public const int KnightEndGame = 305;
        public const int BishopMidGame = 333;
        public const int BishopEndGame = 333;
        public const int RookMidGame = 563;
        public const int RookEndGame = 563;
        public const int QueenMidGame = 950;
        public const int QueenEndGame = 950;

        private static readonly ScoreExt[][] pieceMobility = new ScoreExt[(int)PieceType.NB][]
{
        Array.Empty<ScoreExt>(),
        Array.Empty<ScoreExt>(),
        new ScoreExt[9] {new(-70, -80), //Knight
                        new(-53, -55), new(-12, -10), new(-2, -17), new(3, 5), new(10, 15),
                        new(25, 20), new(38, 25), new(45, 30) },
        new ScoreExt[14] {new(-45, -59), //Bishop
                        new(-20, -25), new(14, -8), new(29, 5), new(40, 20), new(50, 42),
                        new(60, 58), new(62, 65), new(68, 72), new(75, 78), new(83, 78),
                        new(90, 88), new(95, 90), new(100, 100) },
        new ScoreExt[15] {new(-60, -82), //Rook
                        new(-25, -15), new(1, 17), new(3, 42), new(5, 73), new(15, 95),
                        new(20, 108), new(30, 110), new(42, 131), new(42, 142), new(42, 145),
                        new(46, 155), new(50, 160), new(59, 165), new(68, 170) },
        new ScoreExt[28] {new(-29, -49), //Queen
                        new(-16, -29), new(-8, -8), new(-8, 17), new(18, 39), new(25, 54),
                        new(23, 59), new(37, 73), new(41, 76), new(54, 95), new(65, 95),
                        new(68, 101), new(69, 124), new(70, 128), new(70, 132), new(70, 133),
                        new(71, 136), new(72, 140), new(74, 147), new(76, 149), new(90, 153),
                        new(104, 169), new(105, 171), new(106, 171), new(112, 178), new(114, 185),
                        new(114, 187), new(119, 221)},
        new ScoreExt[9] {new(0, 0), //King
                        new(0, 0), new(0, 0), new(0, 0), new(0, 0), new(0, 0),
                        new(0, 0), new(0, 0), new(0, 0)  },
};

        private static readonly ScoreExt[] pieceScores = new ScoreExt[(int)PieceType.NB]
        {
        ScoreExt.Zero,
        new (PawnMidGame, PawnEndGame),
        new (KnightMidGame, KnightEndGame),
        new (BishopMidGame, BishopEndGame),
        new (RookMidGame, RookEndGame),
        new (QueenMidGame, QueenEndGame),
        ScoreExt.Zero,
        };

        public static ScoreExt PieceScore(this PieceType pieceType)
        {
            return pieceScores[(int)pieceType];
        }

        public static ScoreExt PieceMobility(this PieceType pieceType, int attackableSquares)
        {
            return pieceMobility[(int)pieceType][attackableSquares];
        }
    }
}
