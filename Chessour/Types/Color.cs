namespace Chessour.Types
{
    public enum Color
    {
        White, Black, NB
    }

    public static partial class CoreFunctions
    {
        public static bool IsValid(this Color color)
        {
            return color == Color.White || color == Color.Black;
        }

        public static Color Opposite(this Color color)
        {
            return (Color)((int)color ^ (int)Color.Black);
        }

        public static Direction PawnPush(Color side)
        {
            return Direction.North - (16 * (int)side);
        }
    }
}