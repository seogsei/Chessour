using System.Collections.Generic;

namespace Chessour.Search;

internal struct Limits
{
    public List<Move>? searchMoves;

    public long startTime, whiteTime, blackTime, whiteIncrement, blackIncrement, moveTime;
    public int movesToGo, mate, depth, perft;
    public long nodes;
    public bool infinite;

    public Limits()
    {
        startTime = whiteTime = blackTime = whiteIncrement = blackIncrement = moveTime = 0;
        movesToGo = mate = depth = perft = 0;
        nodes = 0;
        infinite = false;
    }

    public bool UseTimeManagement()
    {
        return whiteTime != default || blackTime != default;
    }
}
