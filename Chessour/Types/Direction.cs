namespace Chessour.Types
{
    public enum Direction
    {
        North = 8,
        East = 1,
        South = -North,
        West = -East,

        NorthEast = North + East,
        NorthWest = North + West,
        SouthEast = South + East,
        SouthWest = South + West
    }
}
