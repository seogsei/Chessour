using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chessour.Types
{
    public enum Depth
    {
        TTDepthOffset = -7,
        None,
        QSearch_Recaptures = -5,
        QSearch_NoChecks = -1,
        QSearch_Checks = 0,


        Max = 256,
    }
}
