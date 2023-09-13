namespace Chessour.Search
{
    public class Table2D<T>
    {
        protected Table2D(int width, int height)
        {
            values = new T[width,height];
        }

        private readonly T[,] values;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get(int x, int y)
        {
            return values[x,y];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int x, int y, T value) 
        {
            values[x, y] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetReference(int x, int y)
        {
            return ref values[x, y];
        }

        public void Clear()
        {
            Array.Clear(values);
        }
    }

    public class ButterflyTable : Table2D<int>
    {
        public ButterflyTable() : base(2, 4096) { }
    }
}
