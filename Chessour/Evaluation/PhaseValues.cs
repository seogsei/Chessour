namespace Chessour.Evaluation
{
    public enum Phase
    {
        Pawn = 1,
        Knight = 2,
        Bishop = 2,
        Rook = 4,
        Queen = 8,
        Total = 16 * Pawn + 4 * Knight + 4 * Bishop + 4 * Rook + 2 * Queen,
    }
}
