using System;
using System.Diagnostics;
using Chessour.Types;
using System.Numerics;

using static Chessour.Bitboards;

using static Chessour.Types.Bitboard;
using static Chessour.Types.Color;
using static Chessour.Types.Direction;
using static Chessour.Types.Factory;
using static Chessour.Types.PieceType;

namespace Chessour
{
    class Program
    {
        static ulong lowestRandomGenCount = ulong.MaxValue;

        public static void Main(string[] args)
        {
            Bitboards.Init();
            Position.Init();
            PSQT.Init();
            Evaluation.Init();

            UCI.Run(args);

            Engine.Threads.SetSize(0);
        }

        static ulong CalculateMagics(PieceType pt, Span<MagicStruct> target, Span<Bitboard> table, int seed)
        {
            static Bitboard SliderAttacks(PieceType pt, Square square, Bitboard occupancy)
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

            Random random = new(seed);
            ulong SparseRandom()
            {
                unchecked
                {
                    return (ulong)random.NextInt64() & (ulong)random.NextInt64() & (ulong)random.NextInt64();
                }
            }

            ulong randomGenCount = 0;

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
                m.offset = s == Square.a1 ? 0 : size + target[(int)s - 1].offset;

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

                for (int i = 0; i < size;)
                {
                    for (m.magic = 0; BitOperations.PopCount((ulong)m.mask * m.magic >> 56) < 6;)
                        if (randomGenCount++ < lowestRandomGenCount)
                            m.magic = SparseRandom();
                        else
                            return randomGenCount;

                    for (attemptCount++, i = 0; i < size; i++)
                    {
                        int index = m.GetTableIndex(occupancies[i]);

                        //Check to see if this index is already occupied in this run
                        if (epoch[index - m.offset] < attemptCount)
                        {
                            epoch[index - m.offset] = attemptCount;
                            table[index] = references[i];
                        }
                        //If occupied check to see if both conditions lead to same squares being attacked if not start again
                        else if (table[index] != references[i])
                            break;
                    }
                }
            }
            return randomGenCount;
        }
    }
}
