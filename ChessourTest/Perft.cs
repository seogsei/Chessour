using Chessour;
using Chessour.Search;

namespace ChessourTest
{
    [TestClass]
    public class Perft
    {
        [TestMethod]
        [DataRow("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 7, 3_195_901_860L)]
        [DataRow("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 6, 8_031_647_685L)]
        [DataRow("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 8, 3_009_794_393L)]
        [DataRow("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 6, 706_045_033L)]
        [DataRow("r2q1rk1/pP1p2pp/Q4n2/bbp1p3/Np6/1B3NBn/pPPP1PPP/R3K2R b KQ - 0 1", 6, 706_045_033L)]
        public void PerftTest(string fenString, int depth, long expectedResult)
        {
            Assert.AreEqual(expectedResult, PerftFunction(new(fenString), depth));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2014:Do not use stackalloc in loops", Justification = "<Pending>")]
        private long PerftFunction(Position position, int depth)
        {
            long total = 0;

            Position.StateInfo state = new();
            var moves = MoveGenerators.Legal.Generate(position, stackalloc MoveScore[256]);
            if (depth == 1)
                return moves.Length;

            foreach (var move in moves)
            {
                position.MakeMove(move, state);
                total += depth == 2 ? MoveGenerators.Legal.Generate(position, stackalloc MoveScore[256]).Length
                                    : PerftFunction(position, depth - 1);
                position.Takeback(move);
            }

            return total;
        }
    }
}
