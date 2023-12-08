using System.Numerics;

namespace Chessour.Evaluation
{
    public readonly record struct ScoreTuple(int MidGame, int EndGame) 
        : IAdditionOperators<ScoreTuple, ScoreTuple, ScoreTuple>,
        IUnaryNegationOperators<ScoreTuple, ScoreTuple>,
        IMultiplyOperators<ScoreTuple, int, ScoreTuple>,
        IDivisionOperators<ScoreTuple, int, ScoreTuple>
    {
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