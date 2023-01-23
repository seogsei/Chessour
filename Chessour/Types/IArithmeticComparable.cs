using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chessour.Types
{
    public interface IArithmeticComparable<T> where T : IArithmeticComparable<T>
    {
        static abstract bool operator <(T lhs, T rhs);
        static abstract bool operator >(T lhs, T rhs);
    }
}
