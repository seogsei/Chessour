using System.Runtime.InteropServices;
using System.Numerics;
using System.Text;

namespace Chessour
{
    public enum Bound
    {
        Lower = 1,
        Upper = 2,
        Exact = Upper | Lower,
    }

    class TranspositionTable
    {
        public struct Entry
        {
            internal ulong key64;
            internal ushort move16;
            internal byte depth8;
            internal short eval16;
            internal byte gen8;

            public Key Key { get => (Key)key64; }
            public Move Move { get => (Move)move16; }
            public Depth Depth { get => depth8 - Depth.TTDepthOffset; }
            public Value Evaluation { get => (Value)eval16; }
            public Bound BoundType { get => (Bound)(gen8 & 3); }
            public bool IsPV { get => (gen8 & 4) != 0; }
            public int Generation { get => gen8 >> 3; }

            public void Save(Key key, bool isPV, Move move, Depth depth, Bound boundType, Value evaluation)
            {
                if (move != Move.None || (ulong)key != key64)
                {
                    move16 = (ushort)move;
                }
                if (boundType == Bound.Exact
                    || (ulong)key != key64
                    || (depth - (int)Depth.TTDepthOffset + (isPV ? 2 : 0)) > (Depth)depth8 - 4)
                {
                    Debug.Assert(depth > Depth.TTDepthOffset);
                    Debug.Assert(depth < 256 + Depth.TTDepthOffset);

                    key64 = (ulong)Key;
                    move16 = (ushort)move;
                    depth8 = (byte)(Depth + (int)Depth.TTDepthOffset);
                    eval16 = (short)evaluation;
                    gen8 = (byte)(Engine.TTTable.Generation + (isPV ? 4 : 0) + boundType);
                }
            }
        }

        Entry[] entries;

        public int Generation { get; private set; }

        public TranspositionTable(uint initialSizeInMB = 8)
        {
            Resize(initialSizeInMB);

            if (entries is null)
                throw new Exception();
        }

        public void NewSearch()
        {
            Generation += 1 << 3;
        }

        public void Resize(uint sizeInMB)
        {
            Engine.Threads.WaitForSeachFinish();

            //If size is not a power of 2 get the nearest smaller power of 2
            if(!BitOperations.IsPow2(sizeInMB))
                sizeInMB = BitOperations.RoundUpToPowerOf2(sizeInMB / 2);

            nuint entryCount = (nuint)(sizeInMB * 1024 * 1024 / Marshal.SizeOf(typeof(Entry)));

            entries = new Entry[entryCount];
        }
      
        public void Clear()
        {
            Array.Clear(entries);
        }

        public ref Entry ProbeTT(Key key, out bool found)
        {
            ref Entry entry = ref GetEntry(key);

            //Entry is either refers to same position or is empty
            if(entry.Key == key || entry.Depth == 0)
            {
                found = entry.depth8 != 0;
                return ref entry;
            }

            ref Entry replace = ref entry;
            if (replace.depth8 != 0)
            {

            }


            found = false;
            return ref replace;
        }

        private ref Entry GetEntry(Key key)
        {
            return ref entries[(ulong)key & ((ulong)entries.Length - 1)];
        }
    }

    class HashTable<T> where T : struct
    {
        T[] entries;

        public void Resize(int sizeInMB)
        {
            Engine.Threads.WaitForSeachFinish();

            nuint entryCount = (nuint)(sizeInMB * 1024 * 1024 / Marshal.SizeOf(typeof(T)));

            if (!BitOperations.IsPow2(entryCount))
                entryCount = BitOperations.RoundUpToPowerOf2(entryCount / 2);

            entries = new T[entryCount];
        }

        public ref T Probe(Key key)
        {
            return ref entries[(nuint)key & ((nuint)entries.Length - 1)];
        }
    }
}
