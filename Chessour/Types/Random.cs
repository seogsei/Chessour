namespace Chessour.Types
{
    public static partial class Core
    {
        public static ulong NextUInt64(this Random random)
        {
            unchecked
            {
                return (ulong)random.NextInt64();
            }
        }
        public static ulong SparseUInt64(this Random random)
        {
            return random.NextUInt64() & random.NextUInt64() & random.NextUInt64();
        }
    }
}
