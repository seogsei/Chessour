using System;
using System.Numerics;

namespace Chessour.Types
{
    public enum Bitboard : ulong
    {
        Empty,
        All = ~Empty,

        FileA = 0x0101010101010101ul,
        FileB = FileA << 1,
        FileC = FileA << 2,
        FileD = FileA << 3,
        FileE = FileA << 4,
        FileF = FileA << 5,
        FileG = FileA << 6,
        FileH = FileA << 7,

        Rank1 = 0xFF,
        Rank2 = Rank1 << (8 * 1),
        Rank3 = Rank1 << (8 * 2),
        Rank4 = Rank1 << (8 * 3),
        Rank5 = Rank1 << (8 * 4),
        Rank6 = Rank1 << (8 * 5),
        Rank7 = Rank1 << (8 * 6),
        Rank8 = Rank1 << (8 * 7),
    }

    public struct BitboardEnumerator
    {
        private Bitboard bits;
        public Square Current { get; private set; }

        public BitboardEnumerator(Bitboard b)
        {
            bits = b;
            Current = 0;
        }

        public bool MoveNext()
        {
            if (bits != 0)
            {
                Current = bits.PopSquare();
                return true;
            }
            else
                return false;
        }
    }

    public static class BitboardExtensions
    {
        public static BitboardEnumerator GetEnumerator(this Bitboard b)
        {
            return new(b);
        }

        public static Bitboard SafeStep(this Square square, Direction direction)
        {
            Square to = square.Shift(direction);

            return to.IsValid() && Bitboards.Distance(square, to) <= 2 ? to.ToBitboard() : 0;
        }

        public static Bitboard Shift(this Bitboard bitboard, Direction direction)
        {
            return direction switch
            {
                Direction.North => bitboard.ShiftNorth(),
                Direction.South => bitboard.ShiftSouth(),
                Direction.East => bitboard.ShiftEast(),
                Direction.West => bitboard.ShiftWest(),
                Direction.NorthEast => bitboard.ShiftNorthEast(),
                Direction.NorthWest => bitboard.ShiftNorthWest(),
                Direction.SouthEast => bitboard.ShiftSouthEast(),
                Direction.SouthWest => bitboard.ShiftSouthWest(),

                _ => throw new InvalidOperationException()
            };
        }

        public static Bitboard ShiftNorth(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard << 8);
        }

        public static Bitboard ShiftSouth(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard >> 8);
        }

        public static Bitboard ShiftEast(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard << 1) & ~Bitboard.FileA;
        }

        public static Bitboard ShiftWest(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard >> 1) & ~Bitboard.FileH;
        }

        public static Bitboard ShiftNorthEast(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard << 9) & ~Bitboard.FileA;
        }

        public static Bitboard ShiftNorthWest(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard << 7) & ~Bitboard.FileH;
        }

        public static Bitboard ShiftSouthEast(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard >> 7) & ~Bitboard.FileA;
        }

        public static Bitboard ShiftSouthWest(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard >> 9) & ~Bitboard.FileH;
        }

        public static bool MoreThanOne(this Bitboard bitboard)
        {
            return (bitboard & (bitboard - 1)) != 0;
        }

        public static Square LeastSignificantSquare(this Bitboard bitboard)
        {
            return (Square)BitOperations.TrailingZeroCount((ulong)bitboard);
        }

        public static Bitboard LeastSignificantSquareBitboard(this Bitboard bitboard)
        {
            return bitboard ^ (bitboard - 1);
        }

        public static Square PopSquare(ref this Bitboard bitboard)
        {
            Square square = bitboard.LeastSignificantSquare(); //Gets the index of least significant bit

            bitboard &= bitboard - 1; //Resets the least significant bit

            return square;
        }

        public static int PopulationCount(this Bitboard bitboard)
        {
            return BitOperations.PopCount((ulong)bitboard);
        }
    }

    public static partial class Factory
    {
        public static Bitboard MakeBitboard(Square square)
        {
            return (Bitboard)(1ul << (int)square);
        }

        public static Bitboard MakeBitboard(File file)
        {
            return (Bitboard)((ulong)Bitboard.FileA << (int)file);
        }

        public static Bitboard MakeBitboard(Rank rank)
        {
            return (Bitboard)((ulong)Bitboard.Rank1 << ((int)rank * 8));
        }
    }
}
