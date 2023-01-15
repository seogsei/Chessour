using System;
using System.Runtime.CompilerServices;

namespace Chessour.Types
{
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
        None = -1
    }

    public static class SquareExtensions
    {
        public static bool IsValid(this Square square)
        {
            return square >= Square.a1 && square <= Square.h8;
        }

        public static Square RelativeTo(this Square square, Color side)
        {
            return square ^ (Square)(56 * (int)side);
        }
        public static Square FlipRank(this Square square)
        {
            return square ^ Square.a8;
        }
        public static Square Shift(this Square square, Direction direction)
        {
            return square + (int)direction;
        }
        public static Square NegativeShift(this Square square, Direction direction)
        {
            return square - (int)direction;
        }
        public static File FileOf(this Square square)
        {
            return (File)((int)square & 7);
        }
        public static int EdgeDistance(this File file)
        {
            return Math.Min((int)file, (int)File.h - (int)file);
        }
        public static int EdgeDistance(this Rank rank)
        {
            return Math.Min((int)rank, (int)Rank.R8 - (int)rank);
        }
        public static Rank RankOf(this Square square)
        {
            return (Rank)((int)square >> 3);
        }
        public static Bitboard ToBitboard(this Square square)
        {
            return Factory.MakeBitboard(square);
        }
    }

    public static partial class Factory
    {
        public static Square MakeSquare(File file, Rank rank)
        {
            return (Square)(((int)rank << 3) | (int)file);
        }
    }
}
