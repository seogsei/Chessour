using System.Collections.Generic;
using System.Numerics;

namespace Chessour.Utilities
{
    public static partial class InsertionSort
    {
        public static void Sort<T>(Span<T> buffer) where T : IComparisonOperators<T, T, bool>
        {
            PartialSort(buffer, 0, buffer.Length);
        }
        public static void PartialSort<T>(Span<T> buffer, int start, int end) where T : IComparisonOperators<T, T, bool>
        {
            for (int i = start + 1; i < end; i++)
            {
                T key = buffer[i];

                int j = i - 1;
                for (; j >= start && buffer[j] < key; j--)
                    buffer[j + 1] = buffer[j];

                buffer[j + 1] = key;
            }
        }

        public static void Sort<T>(IList<T> buffer) where T : IComparisonOperators<T, T, bool>
        {
            PartialSort(buffer, 0, buffer.Count);
        }
        public static void PartialSort<T>(IList<T> buffer, int start, int end) where T : IComparisonOperators<T, T, bool>
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
