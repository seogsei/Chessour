using Chessour.Utilities;
using System.Numerics;
using Pext = System.Runtime.Intrinsics.X86.Bmi2.X64;
using static Chessour.Direction;
using static Chessour.PieceType;

namespace Chessour
{
    public enum Bitboard : ulong
    {
        Empty,
        All = ~Empty,

        FileA = 0x0101010101010101UL,
        FileB = FileA << 1,
        FileC = FileA << 2,
        FileD = FileA << 3,
        FileE = FileA << 4,
        FileF = FileA << 5,
        FileG = FileA << 6,
        FileH = FileA << 7,

        Rank1 = 0xFFUL,
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

        private static readonly Bitboard[] whitePawnAttacks = new Bitboard[(int)Square.NB];
        private static readonly Bitboard[] blackPawnAttacks = new Bitboard[(int)Square.NB];
        private static readonly Bitboard[] knightAttacks = new Bitboard[(int)Square.NB];
        private static readonly Bitboard[] bishopAttacks = new Bitboard[(int)Square.NB];
        private static readonly Bitboard[] rookAttacks = new Bitboard[(int)Square.NB];
        private static readonly Bitboard[] queenAttacks = new Bitboard[(int)Square.NB];
        private static readonly Bitboard[] kingAttacks = new Bitboard[(int)Square.NB];
        private static readonly Bitboard[][] pseudoAttack = new Bitboard[(int)PieceType.NB][]
        {
            whitePawnAttacks,
            blackPawnAttacks,
            knightAttacks,
            bishopAttacks,
            rookAttacks,
            queenAttacks,
            kingAttacks
        };

        private static readonly MagicStruct[] rookMagics = new MagicStruct[(int)Square.NB];
        private static readonly MagicStruct[] bishopMagics = new MagicStruct[(int)Square.NB];

        static Bitboards()
        {
            static void CalculateMagics(bool rook, MagicStruct[] target)
            {
                XORSHift64 prng = new(4661);

                int size = 0;
                int attemptCount = 0;

                Span<Bitboard> occupancies = stackalloc Bitboard[4096];
                Span<Bitboard> references = stackalloc Bitboard[4096];
                Span<int> epoch = stackalloc int[4096];
                epoch.Clear();

                for (Square square = Square.a1; square <= Square.h8; square++)
                {
                    Bitboard irrelevant = ((Bitboard.Rank1 | Bitboard.Rank8) & ~square.GetRank().ToBitboard())
                                        | ((Bitboard.FileA | Bitboard.FileH) & ~square.GetFile().ToBitboard());

                    ref MagicStruct magicStruct = ref target[(int)square];
                    magicStruct.mask = SliderAttacks(rook, square, 0) & ~irrelevant;
                    magicStruct.shift = 64 - magicStruct.mask.PopulationCount();

                    magicStruct.attacks = new Bitboard[1ul << magicStruct.mask.PopulationCount()];

                    size = 0;
                    Bitboard b = 0;

                    //Carry rippler trick to enumerate all subsets
                    do
                    {
                        occupancies[size] = b;
                        references[size] = SliderAttacks(rook, square, b);

                        if (Pext.IsSupported)
                            magicStruct.attacks[magicStruct.CalculateIndex(b)] = references[size];

                        size++;
                        b = ((ulong)b - magicStruct.mask) & magicStruct.mask;
                    } while (b != 0);

                    if (Pext.IsSupported)
                        continue;

                    for (int i = 0; i < size;)
                    {
                        for (magicStruct.magic = 0; BitOperations.PopCount(((ulong)magicStruct.mask * magicStruct.magic) >> 56) < 6;)
                            magicStruct.magic = prng.SparseUInt64();

                        for (attemptCount++, i = 0; i < size; i++)
                        {
                            //Span doesn't allows us to index using UInt64
                            int index = (int)magicStruct.CalculateIndex(occupancies[i]);

                            //Check to see if this index is already occupied in this run
                            if (epoch[index] < attemptCount)
                            {
                                epoch[index] = attemptCount;
                                magicStruct.attacks[index] = references[i];
                            }
                            //If occupied check to see if both conditions lead to same squares being attacked if not start again
                            else if (magicStruct.attacks[index] != references[i])
                                break;
                        }
                    }
                }
            }
            static Bitboard SliderAttacks(bool rook, Square square, Bitboard occupancy)
            {
                Bitboard attacks = 0;

                Span<Direction> directions = rook ? stackalloc Direction[] { North, East, West, South }
                                                  : stackalloc Direction[] { NorthEast, NorthWest, SouthWest, SouthEast };

                foreach (Direction direction in directions)
                {
                    Square s = square;

                    while (s.SafeStep(direction) != 0 && (occupancy & s.ToBitboard()) == 0)
                        attacks |= (s += (int)direction).ToBitboard();
                }

                return attacks;
            }

            for (Square square = Square.a1; square < Square.NB; square++)
                for (Square other = Square.a1; other < Square.NB; other++)
                    distance[(int)square, (int)other] = Math.Max(Math.Abs(square.GetFile() - other.GetFile()), Math.Abs(square.GetRank() - other.GetRank()));

            CalculateMagics(false, bishopMagics);
            CalculateMagics(true, rookMagics);

            for (Square square = Square.a1; square < Square.NB; square++)
            {
                whitePawnAttacks[(int)square] = square.SafeStep(NorthEast) | square.SafeStep(NorthWest); //White pawn attacks
                blackPawnAttacks[(int)square] = square.SafeStep(SouthEast) | square.SafeStep(SouthWest); //Black pawn attacks

                foreach (Direction d in stackalloc[] { (Direction)17, (Direction)15, (Direction)10, (Direction)6, (Direction)(-6), (Direction)(-10), (Direction)(-15), (Direction)(-17) })
                    knightAttacks[(int)square] |= square.SafeStep(d); //>Knight

                queenAttacks[(int)square] |= bishopAttacks[(int)square] = BishopAttacks(square, 0); //Bishop
                queenAttacks[(int)square] |= rookAttacks[(int)square] = RookAttacks(square, 0); //Rook

                foreach (Direction d in stackalloc[] { North, East, West, South, NorthEast, NorthWest, SouthEast, SouthWest })
                    kingAttacks[(int)square] |= square.SafeStep(d); //King

                for (Square other = Square.a1; other < Square.NB; other++)
                {
                    if ((bishopAttacks[(int)square] & other.ToBitboard()) != 0)
                    {
                        line[(int)square, (int)other] = (BishopAttacks(square, 0) & BishopAttacks(other, 0)) | square.ToBitboard() | other.ToBitboard();
                        between[(int)square, (int)other] = BishopAttacks(square, other.ToBitboard()) & BishopAttacks(other, square.ToBitboard());
                    }

                    if ((rookAttacks[(int)square] & other.ToBitboard()) != 0)
                    {
                        line[(int)square, (int)other] = (RookAttacks(square, 0) & RookAttacks(other, 0)) | square.ToBitboard() | other.ToBitboard();
                        between[(int)square, (int)other] = RookAttacks(square, other.ToBitboard()) & RookAttacks(other, square.ToBitboard());
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Distance(Square square, Square other)
        {
            return distance[(int)square, (int)other];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard Between(Square square, Square other)
        {
            return between[(int)square, (int)other];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard Line(Square square, Square other)
        {
            return line[(int)square, (int)other];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Alligned(Square square1, Square square2, Square square3)
        {
            return (Line(square1, square2) & square3.ToBitboard()) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard WhitePawnAttacks(Square square)
        {
            return whitePawnAttacks[(int)square];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard BlackPawnAttacks(Square square)
        {
            return blackPawnAttacks[(int)square];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard KnightAttacks(Square square)
        {
            return knightAttacks[(int)square];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard BishopAttacks(Square square)
        {
            return bishopAttacks[(int)square];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard BishopAttacks(Square square, Bitboard occupancy)
        {
            return bishopMagics[(int)square].GetAttack(occupancy);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard RookAttacks(Square square)
        {
            return rookAttacks[(int)square];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard RookAttacks(Square square, Bitboard occupancy)
        {
            return rookMagics[(int)square].GetAttack(occupancy);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard QueenAttacks(Square square)
        {
            return queenAttacks[(int)square];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard QueenAttacks(Square square, Bitboard occupancy)
        {
            return BishopAttacks(square, occupancy) | RookAttacks(square, occupancy);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard KingAttacks(Square square)
        {
            return kingAttacks[(int)square];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard Attacks(PieceType pieceType, Square square, Bitboard occupancy)
        {
            return pieceType switch
            {
                Bishop => BishopAttacks(square, occupancy),
                Rook => RookAttacks(square, occupancy),
                Queen => RookAttacks(square, occupancy) | BishopAttacks(square, occupancy),
                _ => Attacks(pieceType, square),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard Attacks(PieceType pieceType, Square square)
        {
            Debug.Assert(pieceType != None && pieceType != Pawn, "Invalid piece type");

            return pseudoAttack[(int)pieceType][(int)square];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard PawnAttacks(Color side, Square square)
        {
            return pseudoAttack[(int)side][(int)square];
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
                //Pext
                if (Pext.IsSupported)
                    return Pext.ParallelBitExtract((ulong)occupancy, (ulong)mask);

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

    public static class BitboardExtensions
    {
        public static Bitboard SafeStep(this Square square, Direction direction)
        {
            Square to = square + (int)direction;

            return to.IsValid() && Bitboards.Distance(square, to) <= 2 ? to.ToBitboard() : 0;
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
        public static bool Contains(this Bitboard bitboard, Square square)
        {
            return (bitboard & ToBitboard(square)) != 0;
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
        public static BitboardEnumerator GetEnumerator(this Bitboard b) => new(b);

        public struct BitboardEnumerator
        {
            private Bitboard bitboard;

            public Square Current { get; private set; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BitboardEnumerator(Bitboard bitboard)
            {
                this.bitboard = bitboard;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {   
                if (bitboard == 0) return false;

                Current = bitboard.LeastSignificantSquare(); //Gets the index of least significant bit

                bitboard &= bitboard - 1; //Resets the least significant bit

                return true;
            }
        }
    }
}
