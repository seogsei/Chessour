using System.Numerics;

using static Chessour.Types.Color;
using static Chessour.Types.Direction;
using static Chessour.Types.PieceType;

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

    static class Bitboards
    {
        public struct BitboardEnumerator
        {
            private Bitboard bits;
            public Square Current { get; private set; }

            public BitboardEnumerator(Bitboard b)
            {
                bits = b;
            }

            public bool MoveNext()
            {
                if (bits != 0)
                {
                    Current = PopSquare(ref bits);
                    return true;
                }
                else
                    return false;
            }
        }
        struct MagicStruct
        {
            public Bitboard mask;
            public ulong magic;
            public int shift;
            public Bitboard[] attacks;

            public readonly int CalculateIndex(Bitboard occupancy)
            {
                return (int)((ulong)(occupancy & mask) * magic >> shift);
            }

            public readonly Bitboard GetAttack(Bitboard occupancy)
            {
                return attacks[(int)((ulong)(occupancy & mask) * magic >> shift)];
            }
        }

        static readonly MagicStruct[] rookMagics = new MagicStruct[(int)Square.NB];
        static readonly MagicStruct[] bishopMagics = new MagicStruct[(int)Square.NB];

        static readonly int[,] distance = new int[(int)Square.NB, (int)Square.NB];
        static readonly Bitboard[,] between = new Bitboard[(int)Square.NB, (int)Square.NB];
        static readonly Bitboard[,] line = new Bitboard[(int)Square.NB, (int)Square.NB];
        static readonly Bitboard[,] pseudoAttacks = new Bitboard[(int)PieceType.NB, (int)Square.NB];

        public static int Distance(Square s1, Square s2)
        {
            return distance[(int)s1, (int)s2];
        }

        public static Bitboard Between(Square s1, Square s2)
        {
            return between[(int)s1, (int)s2];
        }

        public static Bitboard Line(Square s1, Square s2)
        {
            return line[(int)s1, (int)s2];
        }

        public static bool Alligned(Square s1, Square s2, Square s3)
        {
            return (Line(s1, s2) & s3.ToBitboard()) != 0;
        }

        public static BitboardEnumerator GetEnumerator(this Bitboard b)
        {
            return new(b);
        }

        public static Bitboard SafeStep(this Square square, Direction direction)
        {
            Square to = square.Shift(direction);

            return IsValid(to) && Distance(square, to) <= 2 ? to.ToBitboard() : 0;
        }

        public static Bitboard Shift(this Bitboard bitboard, Direction direction)
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


        public static Square PopSquare(ref Bitboard bitboard)
        {
            Square square = bitboard.LeastSignificantSquare(); //Gets the index of least significant bit

            bitboard &= bitboard - 1; //Resets the least significant bit

            return square;
        }

        public static Square LeastSignificantSquare(this Bitboard bitboard)
        {
            return (Square)BitOperations.TrailingZeroCount((ulong)bitboard);
        }

        public static Bitboard LeastSignificantBit(this Bitboard bitboard)
        {
            return bitboard ^ (bitboard - 1);
        }

        public static int PopulationCount(this Bitboard bitboard)
        {
            return BitOperations.PopCount((ulong)bitboard);
        }

        public static bool MoreThanOne(this Bitboard bitboard)
        {
            return (bitboard & (bitboard - 1)) != 0;
        }

        public static Bitboard PawnAttacks(Color side, Square square)
        {
            Debug.Assert(side.IsValid() && IsValid(square));

            return pseudoAttacks[(int)side, (int)square];
        }

        public static Bitboard Attacks(PieceType pieceType, Square square)
        {
            Debug.Assert(pieceType != None && pieceType != Pawn, "Invalid piece type");

            return pseudoAttacks[(int)pieceType, (int)square];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard Attacks(PieceType pt, Square s, Bitboard occupancy)
        {
            return pt switch
            {
                Bishop => bishopMagics[(int)s].GetAttack(occupancy),
                Rook => rookMagics[(int)s].GetAttack(occupancy),
                Queen => bishopMagics[(int)s].GetAttack(occupancy) | rookMagics[(int)s].GetAttack(occupancy),
                _ => Attacks(pt, s),
            };
        }

        public static Bitboard ToBitboard(this Square square)
        {
            return (Bitboard)(1ul << (int)square);
        }
        
        public static Bitboard ToBitboard(this File file)
        {
            return (Bitboard)((ulong)Bitboard.FileA << (int)file);
        }

        public static Bitboard ToBitboard(this Rank rank)
        {
            return (Bitboard)((ulong)Bitboard.Rank1 << ((int)rank * 8));
        }


        public static void Init() { }

        static Bitboards()
        {
            static void CalculateMagics(PieceType pt, MagicStruct[] target)
            {
                Random random = new(4661);

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

                    size = 0;
                    Bitboard b = 0;

                    //Carry rippler trick to enumerate all subsets
                    do
                    {
                        occupancies[size] = b;
                        references[size] = SliderAttacks(pt, s, b);

                        size++;
                        b = (ulong)b - m.mask & m.mask;
                    } while (b != 0);

                    m.attacks = new Bitboard[size];

                    for (int i = 0; i < size;)
                    {
                        for (m.magic = 0; BitOperations.PopCount((ulong)m.mask * m.magic >> 56) < 6;)
                            m.magic = random.SparseUInt64();

                        for (attemptCount++, i = 0; i < size; i++)
                        {
                            int index = m.CalculateIndex(occupancies[i]);

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
                        attacks |= (sq = sq.Shift(d)).ToBitboard();
                }

                return attacks;
            }

            for (Square s1 = Square.a1; s1 <= Square.h8; s1++)
                for (Square s2 = Square.a1; s2 <= Square.h8; s2++)
                    distance[(int)s1, (int)s2] = Math.Max(Math.Abs(s1.GetFile() - s2.GetFile()), Math.Abs(s1.GetRank() - s2.GetRank()));

            CalculateMagics(Bishop, bishopMagics);
            CalculateMagics(Rook, rookMagics);

            for (Square s1 = Square.a1; s1 <= Square.h8; s1++)
            {
                pseudoAttacks[(int)White, (int)s1] = s1.SafeStep(NorthEast) | s1.SafeStep(NorthWest); //White pawn attacks
                pseudoAttacks[(int)Black, (int)s1] = s1.SafeStep(SouthEast) | s1.SafeStep(SouthWest); //Black pawn attacks

                foreach (Direction d in stackalloc[] { (Direction)17, (Direction)15, (Direction)10, (Direction)6, (Direction)(-6), (Direction)(-10), (Direction)(-15), (Direction)(-17) })
                    pseudoAttacks[(int)Knight, (int)s1] |= s1.SafeStep(d); //>Knight

                pseudoAttacks[(int)Queen, (int)s1] |= pseudoAttacks[(int)Bishop, (int)s1] = Attacks(Bishop, s1, 0); //Bishop
                pseudoAttacks[(int)Queen, (int)s1] |= pseudoAttacks[(int)Rook, (int)s1] = Attacks(Rook, s1, 0); //Rook

                foreach (Direction d in stackalloc[] { North, East, West, South, NorthEast, NorthWest, SouthEast, SouthWest })
                    pseudoAttacks[(int)King, (int)s1] |= s1.SafeStep(d); //King


                foreach (PieceType pt in stackalloc[] { Bishop, Rook })
                    for (Square s2 = Square.a1; s2 <= Square.h8; s2++)
                        if ((pseudoAttacks[(int)pt, (int)s1] & s2.ToBitboard()) != 0)
                        {
                            line[(int)s1, (int)s2] = Attacks(pt, s1, 0) & Attacks(pt, s2, 0) | s1.ToBitboard() | s2.ToBitboard();
                            between[(int)s1, (int)s2] = Attacks(pt, s1, s2.ToBitboard()) & Attacks(pt, s2, s1.ToBitboard());
                        }
            }
        }
    }
}
