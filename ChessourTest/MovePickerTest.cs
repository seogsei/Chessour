using Chessour;
using Chessour.Search;

namespace ChessourTest
{
    [TestClass]
    public class MovePickerTest
    {
        private static readonly ButterflyTable butterflyTable = new();

        [TestMethod]
        [DataRow("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1")]
        [DataRow("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1")]
        [DataRow("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1")]
        [DataRow("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1")]
        [DataRow("r2q1rk1/pP1p2pp/Q4n2/bbp1p3/Np6/1B3NBn/pPPP1PPP/R3K2R b KQ - 0 1")]
        public void MoveGenerationTest(string fen)
        {
            Position position = new(fen);

            int moveCount = 0;
            MovePicker moves = new(position, Move.None, butterflyTable, stackalloc MoveScore[256]);
            foreach(Move move in moves) 
            {
                if (!position.IsLegal(move))
                    continue;

                moveCount++;
            }

            int expectedMoveCount = MoveGenerators.Legal.Generate(position, stackalloc MoveScore[MoveGenerators.MaxMoveCount]).Length;

            Assert.AreEqual(expectedMoveCount, moveCount);
        }
    }
}
