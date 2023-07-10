using Chessour.Search;

namespace Chessour.Evaluation
{
    public static class ValueConstants
    {
        public const int Draw = 0;
        public const int ExpectedWin = 10000;
        public const int ExpectedLoss = -10000;      
        public const int Mate = 32000;
        public const int Mated = -32000;
        public const int Infinite = 32001;
        public const int NegativeInfinite = -32001;

        public const int MateInMaxPly = Mate - DepthConstants.MAX_PLY;
        public const int MatedInMaxPly = -MateInMaxPly;

        public const int PawnMidGame = 100;
        public const int PawnEndGame = 100;
        public const int KnightMidGame = 305;
        public const int KnightEndGame = 305;
        public const int BishopMidGame = 333;
        public const int BishopEndGame = 333;
        public const int RookMidGame = 563;
        public const int RookEndGame = 563;
        public const int QueenMidGame = 950;
        public const int QueenEndGame = 950;

        public static int MatedIn(int ply)
        {
            return -Mate + ply;
        }

        public static int MateIn(int ply)
        {
            return Mate - ply;
        }
    }
}
