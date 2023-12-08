using System.Numerics;

namespace Chessour.Evaluation
{
    public readonly struct ScoreTuple(int midGame, int endGame) 
        : IAdditionOperators<ScoreTuple, ScoreTuple, ScoreTuple>,
        IUnaryNegationOperators<ScoreTuple, ScoreTuple>,
        IMultiplyOperators<ScoreTuple, int, ScoreTuple>,
        IDivisionOperators<ScoreTuple, int, ScoreTuple>
    {
        private readonly short midGame = (short)midGame;
        private readonly short endGame = (short)endGame;

        public int MidGame => midGame;
        public int EndGame => endGame;

        public static ScoreTuple Zero { get; } = new();

        public static ScoreTuple operator +(ScoreTuple left, ScoreTuple right)
        {
            return new(left.MidGame + right.MidGame, left.EndGame + right.EndGame);
        }

        public static ScoreTuple operator -(ScoreTuple left, ScoreTuple right)
        {
            return new(left.MidGame - right.MidGame, left.EndGame - right.EndGame);
        }

        public static ScoreTuple operator -(ScoreTuple value)
        {
            return new(-value.MidGame, -value.EndGame);
        }

        public static ScoreTuple operator *(ScoreTuple left, int right)
        {
            return new(left.MidGame * right, left.EndGame * right);
        }

        public static ScoreTuple operator /(ScoreTuple left, int right)
        {
            return new(left.MidGame / right, left.EndGame / right);
        }
    }
}