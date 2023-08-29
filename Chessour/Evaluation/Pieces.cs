namespace Chessour.Evaluation
{
    internal static class Pieces
    {
        public const int PawnValue = 100;
        public const int KnightValue = 300;
        public const int BishopValue = 310;
        public const int RookValue = 500;
        public const int QueenValue = 900;

        public static ScoreTuple Pawn { get; } = new(100, 198);
        public static ScoreTuple Knight { get; } = new(305, 458);
        public static ScoreTuple Bishop { get; } = new(333, 500);
        public static ScoreTuple Rook { get; } = new(563, 844);
        public static ScoreTuple Queen { get; } = new(950, 1560);

        private static readonly int[] pieceValues = new int[(int)Piece.NB]
        {
            0, PawnValue, KnightValue, BishopValue, RookValue, QueenValue, 0, 0,
            0, PawnValue, KnightValue, BishopValue, RookValue, QueenValue, 0, 0
        };

        private static readonly ScoreTuple[] pieceScores = new ScoreTuple[(int)Piece.NB]
        {
                ScoreTuple.Zero, Pawn, Knight, Bishop, Rook, Queen, ScoreTuple.Zero, ScoreTuple.Zero,
                ScoreTuple.Zero, Pawn, Knight, Bishop, Rook, Queen, ScoreTuple.Zero, ScoreTuple.Zero,
        };

        private static readonly ScoreTuple[][] pieceMobility = new ScoreTuple[(int)PieceType.NB][]
        {
                Array.Empty<ScoreTuple>(),
                Array.Empty<ScoreTuple>(),
                new ScoreTuple[9] {new(-70, -80), //Knight
                                new(-53, -55), new(-12, -10), new(-2, -17), new(3, 5), new(10, 15),
                                new(25, 20), new(38, 25), new(45, 30) },
                new ScoreTuple[14] {new(-45, -59), //Bishop
                                new(-20, -25), new(14, -8), new(29, 5), new(40, 20), new(50, 42),
                                new(60, 58), new(62, 65), new(68, 72), new(75, 78), new(83, 78),
                                new(90, 88), new(95, 90), new(100, 100) },
                new ScoreTuple[15] {new(-60, -82), //Rook
                                new(-25, -15), new(1, 17), new(3, 42), new(5, 73), new(15, 95),
                                new(20, 108), new(30, 128), new(42, 131), new(42, 132), new(42, 134),
                                new(44, 132), new(42, 130), new(41, 128), new(40, 126) },
                new ScoreTuple[28] {new(-29, -49), //Queen
                                new(-16, -29), new(-8, -8), new(-8, 17), new(18, 39), new(25, 54),
                                new(23, 59), new(37, 73), new(41, 76), new(54, 95), new(65, 95),
                                new(68, 101), new(69, 124), new(70, 128), new(70, 132), new(70, 133),
                                new(71, 136), new(72, 140), new(74, 147), new(76, 149), new(90, 153),
                                new(104, 169), new(105, 171), new(106, 171), new(112, 178), new(114, 185),
                                new(114, 187), new(119, 221)},
                Array.Empty<ScoreTuple>(),
        };


        public static ScoreTuple PieceScore(Piece piece)
        {
            return pieceScores[(int)piece];
        }

        public static ScoreTuple PieceScore(PieceType pieceType)
        {
            return pieceScores[(int)pieceType];
        }

        public static int PieceValue(Piece piece)
        {
            return pieceValues[(int)piece];
        }

        public static int PieceValue(PieceType pieceType)
        {
            return pieceValues[(int)pieceType];
        }

        public static ScoreTuple PieceMobility(PieceType pieceType, int attackableSquares)
        {
            return pieceMobility[(int)pieceType][attackableSquares];
        }
    }
}
