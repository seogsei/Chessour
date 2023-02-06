using System.Numerics;
using System.Runtime.InteropServices;

namespace Chessour.Search;

public enum Bound
{
    Lower = 1,
    Upper = 2,
    Exact = Upper | Lower,
}

internal class TranspositionTable
{
    private Entry[] entries;

    public TranspositionTable(uint initialSizeInMB = 8)
    {
        Resize(initialSizeInMB);

        if (entries is null)
            throw new Exception();
    }

    public int Generation { get; private set; }

    public void NewSearch()
    {
        Generation += 1 << 3;
    }

    public void Clear()
    {
        Array.Clear(entries);
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

        found = false;
        return ref replace;
    }

    private ref Entry GetEntry(Key key)
    {
        return ref entries[key & ((ulong)entries.Length - 1)];
    }

    public struct Entry
    {
        internal ulong key64;
        internal ushort move16;
        internal byte depth8;
        internal short eval16;
        internal byte gen8;

        public Key Key { get => key64; }
        public Move Move { get => new (move16); }
        public Depth Depth { get => depth8 - DepthConstants.TTOffset; }
        public Value Evaluation { get => eval16; }
        public Bound BoundType { get => (Bound)(gen8 & 3); }
        public bool IsPV { get => (gen8 & 4) != 0; }
        public int Generation { get => gen8 >> 3; }

        public void Save(Key key, bool isPV, Move move, Depth depth, Bound boundType, Value evaluation)
        {
            if (move != Move.None || key != key64)
            {
                move16 = (ushort)move.Value;
            }
            if (boundType == Bound.Exact
                || key != key64
                || (depth - DepthConstants.TTOffset + (isPV ? 2 : 0)) > depth8 - 4)
            {
                Debug.Assert(depth > DepthConstants.TTOffset);
                Debug.Assert(depth < 256 + DepthConstants.TTOffset);

                key64 = Key;
                move16 = (ushort)move.Value;
                depth8 = (byte)(Depth + DepthConstants.TTOffset);
                eval16 = (short)evaluation;
                gen8 = (byte)(Engine.TTTable.Generation + (isPV ? 4 : 0) + boundType);
            }
        }
    }
}
