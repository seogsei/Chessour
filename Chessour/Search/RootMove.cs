using Chessour.Utilities;
using System.Collections.Generic;

namespace Chessour.Search;

public class RootMove : IArithmeticComparable<RootMove>
{
    public RootMove(Move move)
    {
        pv = new() { move };
    }

    public List<Move> pv;
    public int Score { get; set; }
    public int PreviousScore { get; set; }
    public int UCIScore { get; set; }
    public bool BoundUpper { get; set; }
    public bool BoundLower { get; set; }
    public int SelectiveDepth { get; set; }

    public Move Move
    {
        get => pv[0];
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
