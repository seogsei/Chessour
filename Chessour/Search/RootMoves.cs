using System.Collections.Generic;

namespace Chessour.Search;

public class RootMoves : List<RootMove>
{
    public bool Contains(Move m)
    {
        foreach (var rm in this) 
            if (rm.Move == m) 
                return true;

        return false;
    }

    public RootMove? Find(Move m)
    {
        foreach (var item in this)
            if (item.Move == m)
                return item;
        
        return null;
    }

    public void Sort(int start = 0)
    {
        Utility.PartialInsertionSort(this, start, Count);
    }
}
