using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chessour.Types
{
    public static partial class Core
    {
        public static void InsertionSort<T>(IList<T> list) where T : IArithmeticComparable<T>
        {
            PartialInsertionSort(list, 0, list.Count);
        }
        public static void PartialInsertionSort<T>(IList<T> list, int start, int end) where T : IArithmeticComparable<T>
        {
            for (int i = 1; i < end; i++)
            {
                T value = list[i];
                int j = i - 1;
                while (j >= start && list[j] < value)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = value;
            }
        }
    }
}
