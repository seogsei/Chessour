using Chessour.Utilities;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Chessour.Search
{
    public enum Bound
    {
        Lower = 1,
        Upper = 2,
        Exact = Upper | Lower,
    }

    internal class TranspositionTable
    {
        public TranspositionTable(uint initialSizeInMB = 64)
        {
            Resize(initialSizeInMB);

            Debug.Assert(array is not null);
        }

        private Entry[] array;
        private int generation;

        public int Generation
        {
            get => generation;
        }

        public void NewSearch()
        {
            generation++;
        }

        public void Clear()
        {
            Array.Clear(array);
        }

        public void Resize(uint sizeInMB)
        {
            Engine.Threads.WaitForSeachFinish();

            nuint entryCount = (nuint)(sizeInMB * 1024 * 1024 / Marshal.SizeOf<Entry>());

            //Round the size to next smaller power of two for hashing reasons
            entryCount = BitOperations.RoundUpToPowerOf2(entryCount / 2);

            array = new Entry[entryCount];
        }

        public ref Entry ProbeTable(Key key, out bool found)
        {
            ref Entry entry = ref GetEntry(key);

            found = entry.Key == key;

            return ref entry;
        }

        private ref Entry GetEntry(Key key)
        {
            return ref array[(ulong)key & ((ulong)array.Length - 1)];
        }

        public int Hashfull()
        {
            XORSHift64 prng = new(1453);

            int counter = 0;

            for(int i = 0; i < 1000; i++)
                if (GetEntry((Key)prng.NextUInt64()).Depth > DepthConstants.TTOffset)
                    counter++;

            return counter;
        }

        public struct Entry
        {
            //Total size is 14 bytes
            //C# will padd this to 16 bytes for allignment reasons

            private Key key64;
            private ushort move16;
            private short evaluation16;
            private byte extras8;
            private byte depth8;       

            public Key Key { 
                get => key64;
                private set => key64 = value;
            }
            public Move Move 
            {
                get => (Move)move16;
                private set => move16 = (ushort)value;
            }
            public int Depth
            {
                get => depth8 + DepthConstants.TTOffset;
                set => depth8 = (byte)(value - DepthConstants.TTOffset);
            }
            public int Evaluation
            {
                get => evaluation16;
                set => evaluation16 = (short)value;
            }

            public Bound BoundType 
            { 
                get => (Bound)(extras8 & 3);
            }
            public bool IsPV 
            { 
                get => (extras8 & 4) != 0;
            }
            public int Generation 
            { 
                get => extras8 >> 3;
            }

            private void WriteExtras(int generation, bool isPV, Bound boundType) 
            {
                extras8 = (byte)((generation << 3) + (isPV ? 4 : 0) + boundType);
            }

            public void Save(Key key, bool isPV, Move move, int depth, Bound boundType, int evaluation)
            {
                if (depth > Depth)
                {
                    Key = key;
                    Move = move;
                    Depth = depth;
                    Evaluation = evaluation;
                    WriteExtras(Engine.TTTable.generation, isPV, boundType);
                }
            }
        }
    }
}