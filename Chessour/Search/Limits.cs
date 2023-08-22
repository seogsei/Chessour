using System.Collections.Generic;

namespace Chessour.Search 
{
    internal sealed class Limits
    {
        public int Perft;

        public List<Move> Moves = new();
        public long StartTime;
        public long WhiteTime;

        public long BlackTime;
        public long WhiteIncrement;
        public long BlackIncrement;
        public long MoveTime;
        public int MovesToGo;

        public int Mate;
        public int Depth;
        public long Nodes;

        public bool Infinite;

        public bool UseTimeManagement()
        {
            return WhiteTime != 0 || BlackTime != 0;
        }
    }
}

