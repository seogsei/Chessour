using static Chessour.Evaluation.ValueConstants;

namespace Chessour.Evaluation
{
    internal static class Pieces
    {
        private static readonly Score[] pieceScores = new Score[(int)PieceType.NB]
        {
            Score.Zero,
            new Score(PawnMidGame, PawnEndGame),
            new Score(KnightMidGame, KnightEndGame),
            new Score(BishopMidGame, BishopEndGame),
            new Score(RookMidGame, RookEndGame),
            new Score(QueenMidGame, QueenEndGame),
            Score.Zero,
        };
        private static readonly Phase[] phaseScores = new Phase[(int)PieceType.NB];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Score PieceScore(this Piece piece)
        {
            return PieceScore(piece.TypeOf());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Score PieceScore(this PieceType piece)
        {
            return pieceScores[(int)piece];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Phase Phase(this Piece piece)
        {
            return Phase(piece.TypeOf());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Phase Phase(this PieceType piece)
        {
            return phaseScores[(int)piece];
        }
    }
}
