namespace Chessour
{
    public enum Color
    {
        White, Black, NB
    }

    public static class ColorExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Flip(this Color color)
        {
            return color ^ Color.Black;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Direction PawnDirection(this Color side)
        {
            return side == Color.White ? Direction.North : Direction.South;

            //return Direction.North + (2 * (int)Direction.South * (int)side);
        }
    }
}
