namespace Chessour.Types
{
    public enum Square
    {
        None = -1,
        a1, b1, c1, d1, e1, f1, g1, h1,
        a2, b2, c2, d2, e2, f2, g2, h2,
        a3, b3, c3, d3, e3, f3, g3, h3,
        a4, b4, c4, d4, e4, f4, g4, h4,
        a5, b5, c5, d5, e5, f5, g5, h5,
        a6, b6, c6, d6, e6, f6, g6, h6,
        a7, b7, c7, d7, e7, f7, g7, h7,
        a8, b8, c8, d8, e8, f8, g8, h8,
        NB
    }

    public enum File
    {
        a, b, c, d, e, f, g, h, NB
    }

    public enum Rank
    {
        R1, R2, R3, R4, R5, R6, R7, R8, NB
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

    public static partial class Core
    {
        public static bool IsValid(Square square)
        {
            return square >= Square.a1 && square <= Square.h8;
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
        public static Square Shift(this Square square, Direction direction)
        {
            return square + (int)direction;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square NegativeShift(this Square square, Direction direction)
        {
            return square - (int)direction;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rank RelativeTo(this Rank rank, Color side)
        {
            return rank ^ (Rank)(7 * (int)side);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square RelativeTo(this Square square, Color side)
        {
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
        public static Direction PawnPush(Color side)
        {
            return side == Color.White ? Direction.North : Direction.South;
        }

        public static int EdgeDistance(this File file)
        {
            return Math.Min((int)file, (int)File.h - (int)file);
        }
     
        public static int EdgeDistance(this Rank rank)
        {
            return Math.Min((int)rank, (int)Rank.R8 - (int)rank);
        }
    }
}