using Chessour;

namespace ChessourTest
{
    public class BitboardTests
    {
        [Test]
        [TestCase(Square.a4, (Bitboard)(1ul << (int)Square.a4))]
        [TestCase(Square.h3, (Bitboard)(1ul << (int)Square.h3))]
        [TestCase(Square.h1, (Bitboard)(1ul << (int)Square.h1))]
        [TestCase(Square.c8, (Bitboard)(1ul << (int)Square.c8))]
        [TestCase(Square.e2, (Bitboard)(1ul << (int)Square.e2))]
        public void ToBitboardSquareTest(Square square, Bitboard expected)
        {
            Bitboard result = square.ToBitboard();

            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
