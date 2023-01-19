namespace Chessour
{
    class FastStack<T>
    {
        readonly T[] arr;
        public int Count { get; private set; }

        public FastStack(int capacity)
        {
            arr = new T[capacity];
            Count = 0;
        }

        public void Push(T value)
        {
            arr[Count++] = value;
        }

        public T Pop()
        {
            return arr[--Count];
        }

        public T Peek()
        {
            return arr[Count - 1];
        }

        public void Clear()
        {
            Count = 0;
        }
    }
}
