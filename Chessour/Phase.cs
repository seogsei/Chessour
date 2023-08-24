namespace Chessour
{
    public enum Phase
    {
        Pawn = 1,
        Knight = 2,
        Bishop = 2,
        Rook = 4,
        Queen = 8,
        Total = (16 * Pawn) + (4 * Knight) + (4 * Bishop) + (4 * Rook) + (2 * Queen),
    }

    public static class PhaseExtensions
    {
        private static readonly Phase[] piecePhases = new Phase[(int)PieceType.NB]
        {
            0,
            Phase.Pawn,
            Phase.Knight,
            Phase.Bishop,
            Phase.Rook,
            Phase.Queen,
            0
        };

        public static Phase PhaseValue(this Piece piece)
        {
            return PhaseValue(piece.TypeOf());
        }
        public static Phase PhaseValue(this PieceType pieceType)
        {
            return piecePhases[(int)pieceType];
        }
    }
}