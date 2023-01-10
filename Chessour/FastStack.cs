namespace Chessour
{
    class FastStack<T>
    {
        T[] arr;
        int pointer;

        public int Count => pointer;

        public FastStack(int capacity)
        {
            arr = new T[capacity];
            pointer = 0;
        }

        public void Push(T value)
        {
            arr[pointer++] = value;
        }

        public T Pop()
        {
            return arr[--pointer];
        }

        public T Peek()
        {
            return arr[pointer - 1];
        }

        public void Clear()
        {
            pointer = 0;
        }
    }
}
