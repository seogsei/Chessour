using System.Runtime.CompilerServices;

namespace Chessour.Types
{
    public enum Color
    {
        White,
        Black,
        NB
    }

    public static class ColorExtensions
    {
        public static Color Opposite(this Color color)
        {
            return (Color)((int)color ^ (int)Color.Black);
        }

        public static Direction PawnPush(this Color side)
        {
            return Direction.North - (16 * (int)side);
        }
    }
}
