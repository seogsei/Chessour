using Chessour.Evaluation;
using Chessour.Utilities;
using System.Collections.Generic;

namespace Chessour.Search
{
    public class RootMove : IArithmeticComparable<RootMove>
    {
        public RootMove(Move move)
        {
            PV.Add(move);
        }

        public List<Move> PV { get; } = new(4);
        public int Score { get; set; }
        public int PreviousScore { get; set; }
        public int UCIScore { get; set; }
        public bool BoundUpper { get; set; }
        public bool BoundLower { get; set; }
        public int SelectiveDepth { get; set; }

        public Move Move
        {
            get => PV[0];
        }

        public Move Refutation
        {
            get => PV[1];
        }

        public static bool operator <(RootMove lhs, RootMove rhs)
        {
            return lhs.Score != rhs.Score ? lhs.Score < rhs.Score
                                          : lhs.PreviousScore < rhs.PreviousScore;
        }

        public static bool operator >(RootMove lhs, RootMove rhs)
        {
            return lhs.Score != rhs.Score ? lhs.Score > rhs.Score
                                          : lhs.PreviousScore > rhs.PreviousScore;
        }
    }

    public static class RootMoveListExtensions 
    {
        public static bool Contains(this List<RootMove> rootMoves, Move m)
        {
            foreach (var rootMove in rootMoves)
                if (rootMove.Move == m)
                    return true;

            return false;
        }

        public static RootMove? Find(this List<RootMove> rootMoves, Move m)
        {
            foreach (var rootMove in rootMoves)
                if (rootMove.Move == m)
                    return rootMove;

            return null;
        }

        public static void Sort(this List<RootMove> rootMoves, int start = 0)
        {
            Utility.PartialInsertionSort(rootMoves, start, rootMoves.Count);
        }
    }
}