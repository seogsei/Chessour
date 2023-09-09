namespace Chessour.Evaluation
{
    internal static class Phase
    {
        public const int PawnPhase = 0;
        public const int KnightPhase = 1;
        public const int BishopPhase = 1;
        public const int RookPhase = 2;
        public const int QueenPhase = 4;

        public const int Total = (16 * PawnPhase) + (4 * KnightPhase) + (4 * BishopPhase) + (4 * RookPhase) + (2 * QueenPhase); //32

        private static readonly int[] piecePhases = new int[(int)Piece.NB]
        {
           0, PawnPhase, KnightPhase, BishopPhase, RookPhase, QueenPhase, 0, 0,
           0, PawnPhase, KnightPhase, BishopPhase, RookPhase, QueenPhase, 0, 0
        };

        public static int PhaseValue(Piece piece)
        {
            return piecePhases[(int)piece];
        }
        public static int PhaseValue(PieceType pieceType)
        {
            return piecePhases[(int)pieceType];
        }
    }
}
