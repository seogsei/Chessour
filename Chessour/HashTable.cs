global using Key = System.UInt64;

using System.Numerics;
using System.Runtime.InteropServices;

namespace Chessour;

internal class HashTable <T>
{
    protected T[] entries;

    public HashTable()
    {
        Resize(1);

        Debug.Assert(entries is not null);
    }

    public virtual void Resize(int sizeInMB)
    {
        uint entryCount = (uint)(sizeInMB * 1024 * 1024 / Marshal.SizeOf<T>());

        if(!BitOperations.IsPow2(entryCount))
            entryCount = BitOperations.RoundUpToPowerOf2(entryCount / 2);

        entries = new T[entryCount];
    }

    private ref T GetEntry(Key key)
    {
        return ref entries[key & ((ulong)entries.Length - 1)];
    }

}
