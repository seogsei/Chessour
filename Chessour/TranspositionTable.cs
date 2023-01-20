using System.Runtime.InteropServices;
using System.Numerics;

namespace Chessour
{
    public enum Bound
    {
        Exact,
        UpperBound,
        LowerBound
    }

    struct TTEntry
    {
        Key key; 
        ushort move;
        short evaluation;
        byte depth;
        byte gen8;

        public Key Key { get => key; }
        public int Depth { get => depth; }
        public Move Move { get => (Move)move; }
        public bool IsPV { get => (gen8 & 4) != 0; }
        public Bound BoundType { get => (Bound)(gen8 & 3); }
        public int Generation { get => (int)gen8 >> 3; }
        public Value Evaluation { get => (Value)evaluation; }

        public void Save(Key key, int depth, Move move, bool isPV, Bound boundType, Value evaluation, int generation)
        {
            if (move != Move.None || key != Key)
                this.move = (ushort)move;

            if (boundType == Bound.Exact
                || key != Key
                || depth - (-7) + (isPV ? 2 : 0) > Depth - 4)
            {
                this.key = key;
                this.evaluation = (short)evaluation;
                this.depth = (byte)depth;
                this.gen8 = (byte)(generation | (isPV ? 4 : 0) | (int)boundType);
            }
        }
    }

    class TranspositionTable
    {
        TTEntry[] entries;

        public int Generation { get; private set; }

        public void Init() { }

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

            nuint entryCount = (nuint)(sizeInMB * 1024 * 1024 / Marshal.SizeOf(typeof(TTEntry)));

            entries = new TTEntry[entryCount];
        }
        public void Clear()
        {
            Array.Clear(entries);
        }

        public ref TTEntry ProbeTT(Key key, out bool found)
        {
            ref TTEntry entry = ref Find(key);

            found = entry.Key == key;
            return ref entry;
        }

        private ref TTEntry Find(Key key)
        {
            return ref entries[(ulong)key & (ulong)entries.Length - 1 ];
        }
    }
}
