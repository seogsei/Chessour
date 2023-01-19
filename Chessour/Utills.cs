using Chessour.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chessour
{
    public static class RandomExtensions
    {
        public static ulong NextUInt64(this Random random)
        {
            unchecked
            {
                return (ulong)random.NextInt64();
            }
        }
        public static ulong SparseUInt64(this Random random) 
        {
            return random.NextUInt64() & random.NextUInt64() & random.NextUInt64();
        }
    }    
}
