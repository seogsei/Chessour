namespace Chessour.Utilities
{
    public struct XORSHift64
    {
        public XORSHift64()
        {
            throw new InvalidOperationException();
        }

        public XORSHift64(ulong seed)
        {
            Debug.Assert(seed != 0);

            number = seed;
        }

        private ulong number;

        public ulong NextUInt64()
        {
            number ^= number >> 12;
            number ^= number << 25;
            number ^= number >> 27;

            return number * 2685821657736338717;
        }

        public ulong SparseUInt64()
        {
            return NextUInt64() & NextUInt64() & NextUInt64();
        }
    }
}
