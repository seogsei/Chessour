namespace Chessour
{
    public enum Key : ulong
    {

    }

    public enum Color
    {
        White, Black, NB
    }

    public enum Piece
    {
        None,
        WhitePawn = PieceType.Pawn, WhiteKnight, WhiteBishop, WhiteRook, WhiteQueen, WhiteKing,
        BlackPawn = PieceType.Pawn + 8, BlackKnight, BlackBishop, BlackRook, BlackQueen, BlackKing,
        NB = 16
    }

    public enum PieceType
    {
        None = 0,
        AllPieces = 0,
        Pawn, Knight, Bishop, Rook, Queen, King,
        NB = 7,
    }

    public enum Square
    {
        a1, b1, c1, d1, e1, f1, g1, h1,
        a2, b2, c2, d2, e2, f2, g2, h2,
        a3, b3, c3, d3, e3, f3, g3, h3,
        a4, b4, c4, d4, e4, f4, g4, h4,
        a5, b5, c5, d5, e5, f5, g5, h5,
        a6, b6, c6, d6, e6, f6, g6, h6,
        a7, b7, c7, d7, e7, f7, g7, h7,
        a8, b8, c8, d8, e8, f8, g8, h8,
        NB,
        None = -1,
    }

    public enum Direction
    {
        North = 8,
        East = 1,
        South = -North,
        West = -East,

        NorthEast = North + East,
        NorthWest = North + West,
        SouthEast = South + East,
        SouthWest = South + West
    }

    public enum File
    {
        a, b, c, d, e, f, g, h, NB
    }

    public enum Rank
    {
        R1, R2, R3, R4, R5, R6, R7, R8, NB
    }

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

    internal static class BoardRepresentation
    {
        private static readonly string pieceToChar = " PNBRQK  pnbrqk";

        public static char PieceToChar(Piece piece)
        {
            return pieceToChar[(int)piece];
        }

        public static bool IsValid(Square square)
        {
            return square >= Square.a1 && square <= Square.h8;
        }

        public static bool IsValid(this Color color)
        {
            return color == Color.White || color == Color.Black;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ColorOf(this Piece piece)
        {
            return (Color)((int)piece >> 3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PieceType TypeOf(this Piece piece)
        {
            return (PieceType)((int)piece & 7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Flip(this Color color)
        {
            return color ^ Color.Black;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Piece Flip(this Piece piece)
        {
            return piece ^ (Piece)8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Piece MakePiece(Color color, PieceType pieceType)
        {
            return (Piece)(((int)color << 3) + (int)pieceType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Direction PawnPush(this Color side)
        {
            //return side == Color.White ? Direction.North : Direction.South;

            return Direction.North + (2 * (int)Direction.South * (int)side);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square MakeSquare(File file, Rank rank)
        {
            return (Square)(((int)rank << 3) + (int)file);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static File GetFile(this Square square)
        {
            return (File)((int)square & 7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rank GetRank(this Square square)
        {
            return (Rank)((int)square >> 3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rank RelativeTo(this Rank rank, Color side)
        {
            return rank ^ (Rank)(7 * (int)side);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EdgeDistance(this File file)
        {
            return Math.Min((int)file, (int)File.h - (int)file);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EdgeDistance(this Rank rank)
        {
            return Math.Min((int)rank, (int)Rank.R8 - (int)rank);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square RelativeTo(this Square square, Color side)
        {
            //return side == Color.White ? square : square ^ Square.a8;

            //This branchless implementation is better
            return square ^ (Square)(56 * (int)side);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square FlipRank(this Square square)
        {
            return square ^ Square.a8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square FlipFile(this Square square)
        {
            return square ^ Square.h1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CastlingRight MakeCastlingRight(Color side, CastlingRight cr)
        {
            return (side == Color.White ? CastlingRight.WhiteSide : CastlingRight.BlackSide) & cr;
        }
    }
}
