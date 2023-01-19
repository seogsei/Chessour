namespace Chessour.Types
{
    enum Phase
    {
        Pawn = 0,
        Knight = 1,
        Bishop = 1,
        Rook = 2,
        Queen = 4,
        Total = 16 * Pawn + 4 * Knight + 4 * Bishop + 4 * Rook + 2 * Queen
    }
}
