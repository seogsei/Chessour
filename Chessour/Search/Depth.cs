global using Depth = System.Int32;

namespace Chessour.Search
{
    public static class DepthConstants
    {
        public const int MAX_PLY = 256;

        public const Depth Max = 256;
        public const Depth TTOffset = -7;
        public const Depth QSRecapture = -5;
        public const Depth QSNoChecks = -1;
        public const Depth QSChecks = 0;
    }
}
