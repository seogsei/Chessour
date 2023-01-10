using System;
using System.Numerics;
using System.Runtime.CompilerServices;

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

    public static class BitboardExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard SafeStep(this Square square, Direction direction)
        {
            Square to = square.Shift(direction);

            return to.IsValid() && Bitboards.Distance(square, to) <= 2 ? to.ToBitboard() : Bitboard.Empty;
        }


        public static Bitboard Shift(this Bitboard bitboard, Direction direction) => direction switch
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftNorth(this Bitboard bitboard) => (Bitboard)((ulong)bitboard << 8);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftSouth(this Bitboard bitboard) => (Bitboard)((ulong)bitboard >> 8);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftEast(this Bitboard bitboard) => (Bitboard)((ulong)bitboard << 1) & ~Bitboard.FileA;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftWest(this Bitboard bitboard) => (Bitboard)((ulong)bitboard >> 1) & ~Bitboard.FileH;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftNorthEast(this Bitboard bitboard) => (Bitboard)((ulong)bitboard << 9) & ~Bitboard.FileA;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftNorthWest(this Bitboard bitboard) => (Bitboard)((ulong)bitboard << 7) & ~Bitboard.FileH;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftSouthEast(this Bitboard bitboard) => (Bitboard)((ulong)bitboard >> 7) & ~Bitboard.FileA;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftSouthWest(this Bitboard bitboard) => (Bitboard)((ulong)bitboard >> 9) & ~Bitboard.FileH;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool MoreThanOne(this Bitboard bitboard) => (bitboard & (bitboard - 1)) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square LeastSignificantSquare(this Bitboard bitboard) => (Square)BitOperations.TrailingZeroCount((ulong)bitboard);
        public static Bitboard LeastSignificantSquareBitboard(this Bitboard bitboard) => bitboard ^ (bitboard - 1);
    }

    public static partial class Factory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard MakeBitboard(Square square) => (Bitboard)(1ul << (int)square);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard MakeBitboard(File file) => (Bitboard)((ulong)Bitboard.FileA << (int)file);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard MakeBitboard(Rank rank) => (Bitboard)((ulong)Bitboard.Rank1 << ((int)rank * 8));
    }
}
