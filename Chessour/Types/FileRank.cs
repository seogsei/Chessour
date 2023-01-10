namespace Chessour.Types
{
    public enum File
    {
        a, b, c, d, e, f, g, h, NB
    }
    public enum Rank
    {
        R1, R2, R3, R4, R5, R6, R7, R8, NB
    }

    public static class FileRankExtensions
    {
        public static Rank RelativeTo(this Rank r, Color c)
        {
            return c == Color.White ? r : 7 - r;
        }
    }
}
