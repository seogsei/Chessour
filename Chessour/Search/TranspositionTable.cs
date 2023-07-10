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
    public TranspositionTable(uint initialSizeInMB = 8)
    {
        Resize(initialSizeInMB);

        if (_array is null)
            throw new Exception();
    }

    private Entry[] _array;
    private int _generation;

    public int Generation
    {
        get => _generation;
    }

    public void NewSearch()
    {
        _generation++;
    }

    public void Clear()
    {
        Array.Clear(_array);
    }

    public void Resize(uint sizeInMB)
    {
        Engine.Threads.WaitForSeachFinish();

        //If size is not a power of 2 get the nearest smaller power of 2
        if (!BitOperations.IsPow2(sizeInMB))
            sizeInMB = BitOperations.RoundUpToPowerOf2(sizeInMB / 2);

        nuint entryCount = (nuint)(sizeInMB * 1024 * 1024 / Marshal.SizeOf(typeof(Entry)));

        _array = new Entry[entryCount];
    }

    public ref Entry ProbeTT(Key key, out bool found)
    {
        ref Entry entry = ref GetEntry(key);

        //Entry is either refers to same position or is empty
        if (entry.Key == key || entry.Depth == 0)
        {
            found = entry._depth8 != 0;
            return ref entry;
        }

        ref Entry replace = ref entry;

        found = false;
        return ref replace;
    }

    private ref Entry GetEntry(Key key)
    {
        return ref _array[(ulong)key & ((ulong)_array.Length - 1)];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Entry
    {
        internal Key _key;
        internal Move _move;
        internal byte _gen8;
        internal byte _depth8;
        internal short _eval16;

        public Key Key { get => _key; }
        public Move Move { get => _move; }
        public int Depth { get => _depth8 - DepthConstants.TTOffset; }
        public int Evaluation { get => _eval16; }
        public Bound BoundType { get => (Bound)(_gen8 & 3); }
        public bool IsPV { get => (_gen8 & 4) != 0; }
        public int Generation { get => _gen8 >> 3; }

        public void Save(Key key, bool isPV, Move move, int depth, Bound boundType, int evaluation)
        {
            if (move != Move.None || key != Key)
            {
                _move = move;
            }
            if (boundType == Bound.Exact
                || key != Key
                || (depth - DepthConstants.TTOffset + (isPV ? 2 : 0)) > _depth8 - 4)
            {
                Debug.Assert(depth > DepthConstants.TTOffset);
                Debug.Assert(depth < 256 + DepthConstants.TTOffset);

                _key = key;
                _move = move;
                _depth8 = (byte)(Depth + DepthConstants.TTOffset);
                _eval16 = (short)evaluation;
                _gen8 = (byte)((Engine.TTTable.Generation << 3) + (isPV ? 4 : 0) + boundType);
            }
        }
    }
}
