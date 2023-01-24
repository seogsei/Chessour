namespace Chessour.Types
{
    public enum Color
    {
        White, Black, NB
    }

    public static partial class Core
    {
        public static bool IsValid(this Color color)
        {
            return color == Color.White || color == Color.Black;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Flip(this Color color)
        {
            return color ^ Color.Black;
        }
    }
}