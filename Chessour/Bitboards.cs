using System;
using System.Numerics;
using Chessour.Types;
using static Chessour.Types.Bitboard;
using static Chessour.Types.Color;
using static Chessour.Types.Direction;
using static Chessour.Types.PieceType;

namespace Chessour
{
    static class Bitboards
    {
        static class Magics
        {
            public struct MagicStruct
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
                    return attacks[CalculateIndex(occupancy)];
                }
            }

            public static readonly MagicStruct[] rookMagics = new MagicStruct[(int)Square.NB];
            public static readonly MagicStruct[] bishopMagics = new MagicStruct[(int)Square.NB];

            public static void Init() { }

            private static void CalculateMagics(PieceType pt, MagicStruct[] target)
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
                    Bitboard irrelevant = ((Bitboard.Rank1 | Bitboard.Rank8) & ~MakeBitboard(s.RankOf()))
                                        | ((Bitboard.FileA | Bitboard.FileH) & ~MakeBitboard(s.FileOf()));

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

            private static Bitboard SliderAttacks(PieceType pt, Square square, Bitboard occupancy)
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

            static Magics()
            {
                CalculateMagics(Bishop, bishopMagics);
                CalculateMagics(Rook, rookMagics);
            }
        }

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

        public static Bitboard PawnAttacks(Color side, Square square)
        {
            Debug.Assert(side.IsValid() && square.IsValid());

            return pseudoAttacks[(int)side, (int)square];
        }

        public static Bitboard Attacks(PieceType pieceType, Square square)
        {
            Debug.Assert(pieceType.IsValid() && square.IsValid());

            Debug.Assert(pieceType != None && pieceType != Pawn, "Invalid piece type");

            return pseudoAttacks[(int)pieceType, (int)square];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard Attacks(PieceType pt, Square s, Bitboard occupancy)
        {
            return pt switch
            {
                Bishop => Magics.bishopMagics[(int)s].GetAttack(occupancy),
                Rook => Magics.rookMagics[(int)s].GetAttack(occupancy),
                Queen => Magics.bishopMagics[(int)s].GetAttack(occupancy) | Magics.rookMagics[(int)s].GetAttack(occupancy),
                _ => Attacks(pt, s),
            };
        }

        public static void Init() { }

        static Bitboards()
        {
            for (Square s1 = Square.a1; s1 <= Square.h8; s1++)
                for (Square s2 = Square.a1; s2 <= Square.h8; s2++)
                    distance[(int)s1, (int)s2] = Math.Max(Math.Abs(s1.FileOf() - s2.FileOf()), Math.Abs(s1.RankOf() - s2.RankOf()));

            Magics.Init();

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
