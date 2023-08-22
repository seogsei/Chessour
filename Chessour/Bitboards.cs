using Chessour.Utilities;
using System.Numerics;
using static Chessour.BoardRepresentation;
using static Chessour.Color;
using static Chessour.Direction;
using static Chessour.PieceType;

namespace Chessour
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

    internal static class Bitboards
    {
        private static readonly int[,] distance = new int[(int)Square.NB, (int)Square.NB];
        private static readonly Bitboard[,] between = new Bitboard[(int)Square.NB, (int)Square.NB];
        private static readonly Bitboard[,] line = new Bitboard[(int)Square.NB, (int)Square.NB];
        private static readonly Bitboard[,] pseudoAttack = new Bitboard[(int)PieceType.NB, (int)Square.NB];

        private static readonly MagicStruct[] rookMagics = new MagicStruct[(int)Square.NB];
        private static readonly MagicStruct[] bishopMagics = new MagicStruct[(int)Square.NB];

        static Bitboards()
        {
            static void CalculateMagics(PieceType pt, MagicStruct[] target)
            {
                XORSHift64 prng = new(4661);

                int size = 0;
                int attemptCount = 0;

                Span<Bitboard> occupancies = stackalloc Bitboard[4096];
                Span<Bitboard> references = stackalloc Bitboard[4096];
                Span<int> epoch = stackalloc int[4096];
                epoch.Clear();

                for (Square s = Square.a1; s <= Square.h8; s++)
                {
                    Bitboard irrelevant = ((Bitboard.Rank1 | Bitboard.Rank8) & ~s.GetRank().ToBitboard())
                                        | ((Bitboard.FileA | Bitboard.FileH) & ~s.GetFile().ToBitboard());

                    ref MagicStruct m = ref target[(int)s];
                    m.mask = SliderAttacks(pt, s, 0) & ~irrelevant;
                    m.shift = 64 - m.mask.PopulationCount();

                    m.attacks = new Bitboard[1ul << m.mask.PopulationCount()];

                    size = 0;
                    Bitboard b = 0;

                    //Carry rippler trick to enumerate all subsets
                    do
                    {
                        occupancies[size] = b;
                        references[size] = SliderAttacks(pt, s, b);

                        if (System.Runtime.Intrinsics.X86.Bmi2.X64.IsSupported)
                            m.attacks[m.CalculateIndex(b)] = references[size];

                        size++;
                        b = ((ulong)b - m.mask) & m.mask;
                    } while (b != 0);

                    if (System.Runtime.Intrinsics.X86.Bmi2.X64.IsSupported)
                        continue;

                    for (int i = 0; i < size;)
                    {
                        for (m.magic = 0; BitOperations.PopCount(((ulong)m.mask * m.magic) >> 56) < 6;)
                            m.magic = prng.SparseUInt64();

                        for (attemptCount++, i = 0; i < size; i++)
                        {
                            //Span doesn't allows us to index using UInt64
                            int index = (int)m.CalculateIndex(occupancies[i]);

                            //Check to see if this index is already occupied in this run
                            if (epoch[index] < attemptCount)
                            {
                                epoch[index] = attemptCount;
                                m.attacks[index] = references[i];
                            }
                            //If occupied check to see if both conditions lead to same squares being attacked if not start again
                            else if (m.attacks[index] != references[i])
                                break;
                        }
                    }
                }
            }
            static Bitboard SliderAttacks(PieceType pt, Square square, Bitboard occupancy)
            {
                Bitboard attacks = 0;

                Span<Direction> rookAttacks = stackalloc Direction[] { North, East, West, South };
                Span<Direction> bishopAttacks = stackalloc Direction[] { NorthEast, NorthWest, SouthWest, SouthEast };

                foreach (Direction d in pt == Rook ? rookAttacks : bishopAttacks)
                {
                    Square sq = square;

                    while (sq.SafeStep(d) != 0 && (occupancy & sq.ToBitboard()) == 0)
                        attacks |= (sq += (int)d).ToBitboard();
                }

                return attacks;
            }

            for (Square square = Square.a1; square < Square.NB; square++)
                for (Square other = Square.a1; other < Square.NB; other++)
                    distance[(int)square, (int)other] = Math.Max(Math.Abs(square.GetFile() - other.GetFile()), Math.Abs(square.GetRank() - other.GetRank()));

            CalculateMagics(Bishop, bishopMagics);
            CalculateMagics(Rook, rookMagics);

            for (Square square = Square.a1; square < Square.NB; square++)
            {
                pseudoAttack[(int)White, (int)square] = square.SafeStep(NorthEast) | square.SafeStep(NorthWest); //White pawn attacks
                pseudoAttack[(int)Black, (int)square] = square.SafeStep(SouthEast) | square.SafeStep(SouthWest); //Black pawn attacks

                foreach (Direction d in stackalloc[] { (Direction)17, (Direction)15, (Direction)10, (Direction)6, (Direction)(-6), (Direction)(-10), (Direction)(-15), (Direction)(-17) })
                    pseudoAttack[(int)Knight, (int)square] |= square.SafeStep(d); //>Knight

                pseudoAttack[(int)Queen, (int)square] |= pseudoAttack[(int)Bishop, (int)square] = BishopAttacks(square, Bitboard.Empty); //Bishop
                pseudoAttack[(int)Queen, (int)square] |= pseudoAttack[(int)Rook, (int)square] = RookAttacks(square, Bitboard.Empty); //Rook

                foreach (Direction d in stackalloc[] { North, East, West, South, NorthEast, NorthWest, SouthEast, SouthWest })
                    pseudoAttack[(int)King, (int)square] |= square.SafeStep(d); //King

                for (Square other = Square.a1; other < Square.NB; other++)
                {
                    if ((pseudoAttack[(int)Bishop, (int)square] & other.ToBitboard()) != 0)
                    {
                        line[(int)square, (int)other] = (BishopAttacks(square, Bitboard.Empty) & BishopAttacks(other, Bitboard.Empty)) | square.ToBitboard() | other.ToBitboard();
                        between[(int)square, (int)other] = BishopAttacks(square, other.ToBitboard()) & BishopAttacks(other, square.ToBitboard());
                    }

                    if ((pseudoAttack[(int)Rook, (int)square] & other.ToBitboard()) != 0)
                    {
                        line[(int)square, (int)other] = (RookAttacks(square, Bitboard.Empty) & RookAttacks(other, Bitboard.Empty)) | square.ToBitboard() | other.ToBitboard();
                        between[(int)square, (int)other] = RookAttacks(square, other.ToBitboard()) & RookAttacks(other, square.ToBitboard());
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Distance(Square s1, Square s2)
        {
            return distance[(int)s1, (int)s2];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard Between(Square s1, Square s2)
        {
            return between[(int)s1, (int)s2];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard Line(Square s1, Square s2)
        {
            return line[(int)s1, (int)s2];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Alligned(Square s1, Square s2, Square s3)
        {
            return (Line(s1, s2) & s3.ToBitboard()) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard PawnAttacks(Color side, Square square)
        {
            return pseudoAttack[(int)side, (int)square];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard Attacks(PieceType pieceType, Square square)
        {
            Debug.Assert(pieceType != None && pieceType != Pawn, "Invalid piece type");

            return pseudoAttack[(int)pieceType, (int)square];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard Attacks(PieceType pt, Square square, Bitboard occupancy)
        {
            return pt switch
            {
                Bishop => BishopAttacks(square, occupancy),
                Rook => RookAttacks(square, occupancy),
                Queen => QueenAttacks(square, occupancy),
                _ => Attacks(pt, square),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard BishopAttacks(Square square, Bitboard occupancy)
        {
            return bishopMagics[(int)square].GetAttack(occupancy);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard RookAttacks(Square square, Bitboard occupancy)
        {
            return rookMagics[(int)square].GetAttack(occupancy);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard QueenAttacks(Square square, Bitboard occupancy)
        {
            return bishopMagics[(int)square].GetAttack(occupancy) | rookMagics[(int)square].GetAttack(occupancy);
        }

        private struct MagicStruct
        {
            public Bitboard[] attacks;
            public Bitboard mask;

            //Required if PEXT is not supported
            public ulong magic;
            public int shift;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly ulong CalculateIndex(Bitboard occupancy)
            {
                if (System.Runtime.Intrinsics.X86.Bmi2.X64.IsSupported)
                    return System.Runtime.Intrinsics.X86.Bmi2.X64.ParallelBitExtract((ulong)occupancy, (ulong)mask);

                //Default fallback
                return ((ulong)(occupancy & mask) * magic) >> shift;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly Bitboard GetAttack(Bitboard occupancy)
            {
                return attacks[CalculateIndex(occupancy)];
            }
        }
    }

    internal static class BitboardExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard SafeStep(this Square square, Direction direction)
        {
            Square to = square + (int)direction;

            return IsValid(to) && Bitboards.Distance(square, to) <= 2 ? to.ToBitboard() : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ToBitboard(this Square square)
        {
            return (Bitboard)(1ul << (int)square);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ToBitboard(this File file)
        {
            return (Bitboard)((ulong)Bitboard.FileA << (int)file);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ToBitboard(this Rank rank)
        {
            return (Bitboard)((ulong)Bitboard.Rank1 << ((int)rank * 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOccupied(this Bitboard bitboard)
        {
            return bitboard != Bitboard.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty(this Bitboard bitboard)
        {
            return bitboard == Bitboard.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this Bitboard bitboard, Square square)
        {
            return IsOccupied(bitboard & ToBitboard(square));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square PopSquare(ref Bitboard bitboard)
        {
            Square square = bitboard.LeastSignificantSquare(); //Gets the index of least significant bit

            bitboard &= bitboard - 1; //Resets the least significant bit

            return square;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard LeastSignificantBit(this Bitboard bitboard)
        {
            return bitboard ^ (bitboard - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square LeastSignificantSquare(this Bitboard bitboard)
        {
            return (Square)BitOperations.TrailingZeroCount((ulong)bitboard);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopulationCount(this Bitboard bitboard)
        {
            return BitOperations.PopCount((ulong)bitboard);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool MoreThanOne(this Bitboard bitboard)
        {
            return (bitboard & (bitboard - 1)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftByDirection(this Bitboard bitboard, Direction direction)
        {
            return direction switch
            {
                North => bitboard.ShiftNorth(),
                South => bitboard.ShiftSouth(),
                East => bitboard.ShiftEast(),
                West => bitboard.ShiftWest(),
                NorthEast => bitboard.ShiftNorthEast(),
                NorthWest => bitboard.ShiftNorthWest(),
                SouthEast => bitboard.ShiftSouthEast(),
                SouthWest => bitboard.ShiftSouthWest(),
                _ => throw new InvalidOperationException()
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftEast(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard << 1) & ~Bitboard.FileA;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftNorth(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard << 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftNorthEast(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard << 9) & ~Bitboard.FileA;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftNorthWest(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard << 7) & ~Bitboard.FileH;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftSouth(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard >> 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftSouthEast(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard >> 7) & ~Bitboard.FileA;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftSouthWest(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard >> 9) & ~Bitboard.FileH;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard ShiftWest(this Bitboard bitboard)
        {
            return (Bitboard)((ulong)bitboard >> 1) & ~Bitboard.FileH;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BitboardEnumerator GetEnumerator(this Bitboard b)
        {
            return new(b);
        }

        public struct BitboardEnumerator
        {
            private Bitboard bitboard;
            private Square current;

            public Square Current
            {
                get => current;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BitboardEnumerator(Bitboard bitboard)
            {
                this.bitboard = bitboard;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                if (!bitboard.IsOccupied())
                    return false;

                current = PopSquare(ref bitboard);
                return true;
            }
        }
    }
}
