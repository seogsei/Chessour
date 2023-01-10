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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Opposite(this Color color) => (Color)((int)color ^ (int)Color.Black);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Direction PawnPush(this Color side) => side == Color.White ? Direction.North : Direction.South;
    }

}
