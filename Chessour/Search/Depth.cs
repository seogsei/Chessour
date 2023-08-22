namespace Chessour.Search
{
    public static class DepthConstants
    {
        public const int MAX_DEPTH = MAX_PLY + TTOffset;
        public const int MAX_PLY = 256;

        public const int Max = 256;
        public const int TTOffset = -7;
        public const int QSRecapture = -5;
        public const int QSNoChecks = -1;
        public const int QSChecks = 0;
    }
}
