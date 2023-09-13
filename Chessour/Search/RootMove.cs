using Chessour.Evaluation;
using Chessour.Utilities;
using System.Collections.Generic;
using System.Numerics;

namespace Chessour.Search
{
    public class RootMove : IComparisonOperators<RootMove, RootMove, bool>
    {
        public RootMove(Move move)
        {
            PV.Add(move);
        }

        public List<Move> PV { get; } = new(4);
        public int Score { get; set; }
        public int PreviousScore { get; set; }
        public int AvarageScore { get; set; } = -Evaluator.InfiniteScore;
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

        public static bool operator <=(RootMove left, RootMove right)
        {
            return left.Score != right.Score ? left.Score <= right.Score
                                             : left.PreviousScore <= right.PreviousScore;
        }
        public static bool operator >=(RootMove left, RootMove right)
        {
            return left.Score != right.Score ? left.Score >= right.Score
                                             : left.PreviousScore >= right.PreviousScore;
        }
        public static bool operator <(RootMove left, RootMove right)
        {
            return left.Score != right.Score ? left.Score < right.Score
                                             : left.PreviousScore < right.PreviousScore;
        }
        public static bool operator >(RootMove left, RootMove right)
        {
            return left.Score != right.Score ? left.Score > right.Score
                                             : left.PreviousScore > right.PreviousScore;
        }
        static bool IEqualityOperators<RootMove, RootMove, bool>.operator ==(RootMove? left, RootMove? right)
        {
            throw new NotImplementedException();
        }
        static bool IEqualityOperators<RootMove, RootMove, bool>.operator !=(RootMove? left, RootMove? right)
        {
            throw new NotImplementedException();
        }
    }

    public static class RootMoveListExtensions
    {
        public static bool Contains(this List<RootMove> rootMoves, Move move)
        {
            foreach (var rootMove in rootMoves)
                if (rootMove.Move == move)
                    return true;

            return false;
        }

        public static RootMove? Find(this List<RootMove> rootMoves, Move move)
        {
            foreach (var rootMove in rootMoves)
                if (rootMove.Move == move)
                    return rootMove;

            return null;
        }

        public static void Sort(this List<RootMove> rootMoves, int start = 0)
        {
            InsertionSort.PartialSort(rootMoves, start, rootMoves.Count);
        }
    }
}