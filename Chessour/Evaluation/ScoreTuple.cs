using System.Numerics;

namespace Chessour.Evaluation
{
    public readonly struct ScoreTuple : IAdditionOperators<ScoreTuple, ScoreTuple, ScoreTuple>, IUnaryNegationOperators<ScoreTuple, ScoreTuple>
    {
        public ScoreTuple(int midGame, int endGame)
        {
            MidGame = midGame;
            EndGame = endGame;
        }

        public readonly int MidGame { get; init; }
        public readonly int EndGame { get; init; }

        public static ScoreTuple Zero => default;

        public static ScoreTuple operator +(ScoreTuple left, ScoreTuple right) => new(left.MidGame + right.MidGame, left.EndGame + right.EndGame);
        public static ScoreTuple operator -(ScoreTuple left, ScoreTuple right) => new(left.MidGame - right.MidGame, left.EndGame - right.EndGame);
        public static ScoreTuple operator -(ScoreTuple value) => new(-value.MidGame, -value.EndGame);
    }
}