using System.Collections.Generic;

namespace Chessour.Utilities
{
    public static partial class Utility
    {
        public static void InsertionSort<T>(Span<T> buffer) where T : IArithmeticComparable<T>
        {
            PartialInsertionSort(buffer, 0, buffer.Length);
        }
        public static void PartialInsertionSort<T>(Span<T> buffer, int start, int end) where T : IArithmeticComparable<T>
        {
            for (int i = start + 1; i < end; i++)
            {
                T key = buffer[i];
                int j = i - 1;
                for (; j >= start && buffer[j] < key; j--)
                {
                    buffer[j + 1] = buffer[j];
                }

                buffer[j + 1] = key;
            }
        }

        public static void InsertionSort<T>(IList<T> buffer) where T : IArithmeticComparable<T>
        {
            PartialInsertionSort(buffer, 0, buffer.Count);
        }
        public static void PartialInsertionSort<T>(IList<T> buffer, int start, int end) where T : IArithmeticComparable<T>
        {
            for (int i = start + 1; i < end; i++)
            {
                T key = buffer[i];
                int j = i - 1;
                for (; j >= start && buffer[j] < key; j--)
                {
                    buffer[j + 1] = buffer[j];
                }

                buffer[j + 1] = key;
            }
        }
    }
}
