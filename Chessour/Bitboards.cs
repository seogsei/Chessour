using Chessour.Types;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using static Chessour.Types.Bitboard;
using static Chessour.Types.Color;
using static Chessour.Types.Direction;
using static Chessour.Types.Factory;
using static Chessour.Types.PieceType;

namespace Chessour
{
    static class Bitboards
    {
        public struct MagicStruct
        {
            public Bitboard mask;
            public ulong magic;
            public int shift;
            public Bitboard[] attacks;

            public int CalculateIndex(Bitboard occupancy)
            {
                return (int)((ulong)(occupancy & mask) * magic >> shift);
            }
            public Bitboard GetAttack(Bitboard occupancy)
            {
                return attacks[CalculateIndex(occupancy)];
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

        public static Bitboard PawnAttacks(Color side, Square s)
        {
            return pseudoAttacks[(int)side, (int)s];
        }
        public static Bitboard Attacks(PieceType pt, Square s)
        {
            return pseudoAttacks[(int)pt, (int)s];
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

        public static int PopCount(Bitboard b)
        {
            return BitOperations.PopCount((ulong)b);
        }
        public static Square LowestSquare(Bitboard b)
        {
            return (Square)BitOperations.TrailingZeroCount((ulong)b);
        }
        public static Square PopSquare(ref Bitboard b)
        {
            Square s = LowestSquare(b); //Gets the index of least significant bit

            b &= b - 1; //Resets the least significant bit

            return s;
        }

        public static BitboardEnumerator GetEnumerator(this Bitboard b) => new(b);
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
                    Current = PopSquare(ref bits);
                    return true;
                }
                else
                    return false;
            }
        }

        public static void Init() { }
        static Bitboards()
        {            
            for (Square s1 = Square.a1; s1 <= Square.h8; s1++)
                for (Square s2 = Square.a1; s2 <= Square.h8; s2++)
                    distance[(int)s1, (int)s2] = Math.Max(Math.Abs(s1.FileOf() - s2.FileOf()), Math.Abs(s1.RankOf() - s2.RankOf()));

            Span<PieceType> bishopAndRook = stackalloc[] { Bishop, Rook };

            Span<Direction> kingAttacks = stackalloc[]
                { North, East, West, South, NorthEast, NorthWest, SouthEast, SouthWest };

            Span<Direction> knightAttacks = stackalloc Direction[] { (Direction)17, (Direction)15, (Direction)10, (Direction)6, (Direction)(-6), (Direction)(-10), (Direction)(-15), (Direction)(-17) };

            CalculateMagics(Bishop, bishopMagics);
            CalculateMagics(Rook, rookMagics);

            for (Square s1 = Square.a1; s1 <= Square.h8; s1++)
            {
                pseudoAttacks[(int)White, (int)s1] = s1.SafeStep(NorthEast) | s1.SafeStep(NorthWest); //White pawn attacks
                pseudoAttacks[(int)Black, (int)s1] = s1.SafeStep(SouthEast) | s1.SafeStep(SouthWest); //Black pawn attacks

                foreach (Direction d in kingAttacks)
                    pseudoAttacks[(int)King, (int)s1] |= s1.SafeStep(d);

                foreach (Direction d in knightAttacks)
                    pseudoAttacks[(int)Knight, (int)s1] |= s1.SafeStep(d);

                pseudoAttacks[(int)Bishop, (int)s1] = Attacks(Bishop, s1, 0); //Bishop
                pseudoAttacks[(int)Rook, (int)s1] = Attacks(Rook, s1, 0); //Rook
                pseudoAttacks[(int)Queen, (int)s1] = pseudoAttacks[(int)Bishop, (int)s1] | pseudoAttacks[(int)Rook, (int)s1];

                foreach (PieceType pt in bishopAndRook)
                    for (Square s2 = Square.a1; s2 <= Square.h8; s2++)
                    {
                        if ((pseudoAttacks[(int)pt, (int)s1] & s2.ToBitboard()) != 0)
                        {
                            line[(int)s1, (int)s2] = Attacks(pt, s1, Empty) & Attacks(pt, s2, Empty) | s1.ToBitboard() | s2.ToBitboard();
                            between[(int)s1, (int)s2] = Attacks(pt, s1, s2.ToBitboard()) & Attacks(pt, s2, s1.ToBitboard());
                        }
                    }
            }
        }

        private static void CalculateMagics(PieceType pt, MagicStruct[] target)
        {        
            Random random = new(4661);
            ulong SparseRandom()
            {
                unchecked
                {
                    return (ulong)random.NextInt64() & (ulong)random.NextInt64() & (ulong)random.NextInt64();
                }
            }


            int size = 0;
            int attemptCount = 0;

            Span<Bitboard> occupancies = stackalloc Bitboard[4096];
            Span<Bitboard> references = stackalloc Bitboard[4096];
            Span<int> epoch = stackalloc int[4096];
            epoch.Clear();

            for (Square s = Square.a1; s <= Square.h8; s++)
            {
                Bitboard irrelevant = ((Rank1 | Rank8) & ~MakeBitboard(s.RankOf()))
                                    | ((FileA | FileH) & ~MakeBitboard(s.FileOf()));

                ref MagicStruct m = ref target[(int)s];
                m.mask = SliderAttacks(pt, s, 0) & ~irrelevant;
                m.shift = 64 - PopCount(m.mask);

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
                        m.magic = SparseRandom();

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
        private static Bitboard SliderAttacks(PieceType pt, Square square, Bitboard occupancy)
        {
            Bitboard attacks = 0;

            Span<Direction> rookAttacks = stackalloc Direction[] { North, East, West, South };
            Span<Direction> bishopAttacks = stackalloc Direction[] { NorthEast, NorthWest, SouthWest, SouthEast };

            foreach (Direction d in pt == Rook ? rookAttacks : bishopAttacks)
            {
                Square sq = square;

                while (sq.SafeStep(d) != Empty && (occupancy & sq.ToBitboard()) == 0)
                    attacks |= (sq = sq.Shift(d)).ToBitboard();
            }

            return attacks;
        }
    }
}
