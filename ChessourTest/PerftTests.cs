using Chessour;
using Chessour.Search;
using System;

namespace ChessourTest
{
    public class PerftTests
    {
        [Test]
        [TestCase("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 6, 119060324ul)]
        [TestCase("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 5, 193690690ul)]
        [TestCase("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 7, 178633661ul)]
        [TestCase("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 6, 706045033ul)]
        [TestCase("1k6/1b6/8/8/7R/8/8/4K2R b K - 0 1", 5, 1063513ul)]
        [TestCase("3k4/3p4/8/K1P4r/8/8/8/8 b - - 0 1", 6, 1134888ul)]
        [TestCase("8/8/4k3/8/2p5/8/B2P2K1/8 w - - 0 1", 6, 1015133ul)]
        [TestCase("8/8/1k6/2b5/2pP4/8/5K2/8 b - d3 0 1", 6, 1440467ul)]
        [TestCase("5k2/8/8/8/8/8/8/4K2R w K - 0 1", 6, 661072ul)]
        [TestCase("3k4/8/8/8/8/8/8/R3K3 w Q - 0 1", 6, 803711ul)]
        [TestCase("r3k2r/1b4bq/8/8/8/8/7B/R3K2R w KQkq - 0 1", 4, 1274206ul)]
        [TestCase("r3k2r/8/3Q4/8/8/5q2/8/R3K2R b KQkq - 0 1", 4, 1720476ul)]
        [TestCase("2K2r2/4P3/8/8/8/8/8/3k4 w - - 0 1", 6, 3821001ul)]
        [TestCase("8/8/1P2K3/8/2n5/1q6/8/5k2 b - - 0 1", 5, 1004658ul)]
        [TestCase("4k3/1P6/8/8/8/8/K7/8 w - - 0 1", 6, 217342ul)]
        [TestCase("K1k5/8/P7/8/8/8/8/8 w - - 0 1", 6, 2217ul)]
        [TestCase("8/k1P5/8/1K6/8/8/8/8 w - - 0 1", 7, 567584ul)]
        [TestCase("8/8/2k5/5q2/5n2/8/5K2/8 b - - 0 1", 4, 23527ul)]
        public void PerftTest(string fen, int depth, ulong expectedResult)
        {
            var position = new Position(fen);

            ulong result = Perft(position, depth, false);

            Assert.That(result, Is.EqualTo(expectedResult));
        }

        private static ulong Perft(Position position, int depth, bool output = true)
        {
            Position.StateInfo state = new();

            ulong branchNodes, totalNodes = 0;
            foreach (Move move in MoveGenerator.Generate(position, stackalloc MoveScore[256]))
            {
                if (output && depth == 1)
                    totalNodes += branchNodes = 1;

                else
                {
                    position.MakeMove(move, state);
                    branchNodes = depth == 2 ? (ulong)MoveGenerator.Generate(position, stackalloc MoveScore[256]).Length
                                             : Perft(position, depth - 1, false);
                    totalNodes += branchNodes;
                    position.Takeback(move);
                }

                if (output)
                    Console.WriteLine($"{UCI.ToLongAlgebraic(move)}: {branchNodes}");
            }

            return totalNodes;
        }
    }
}