namespace Chessour.Utilities
{
    public struct XORSHift64
    {
        public XORSHift64(ulong seed)
        {
            Debug.Assert(seed != 0);

            s = seed;
        }

        private ulong s;

        public ulong NextUInt64()
        {
            s ^= s >> 12;
            s ^= s << 25;
            s ^= s >> 27;

            return s * 2685821657736338717;
        }

        public ulong SparseUInt64()
        {
            return NextUInt64() & NextUInt64() & NextUInt64();
        }
    }

    public static class RandomExtensions
    {
        public static ulong NextUInt64(this Random rand)
        {
            unchecked
            {
                return (ulong)rand.NextInt64();
            }
        }
        public static ulong SparseUInt64(this Random rand)
        {
            return rand.NextUInt64() & rand.NextUInt64() & rand.NextUInt64();
        }
    }
}
