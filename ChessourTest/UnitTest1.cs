using System.Data;

namespace ChessourTest
{
    [TestClass]
    public class PerftTests
    {
        [TestMethod]
        [DataRow("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 6, 119060324L)]
        public void Test(string fenString, int depth, long expectedResult)
        {
            
        }

        [TestMethod]
        private void Perft()
        {

        }
    }
}