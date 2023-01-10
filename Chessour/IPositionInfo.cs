using Chessour.Types;

namespace Chessour
{
    public interface IPositionInfo
    {
        Piece PieceAt(Square s);
        Color ActiveColor { get; }
        bool CanCastle(CastlingRight cr);
        Square EnPassantSquare { get; }
        int FiftyMoveCounter { get; }
        int FullMoveCounter { get; }
    }
}
