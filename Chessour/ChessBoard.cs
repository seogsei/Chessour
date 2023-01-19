using static Chessour.Types.Color;
using static Chessour.Types.PieceType;

namespace Chessour
{
    public class ChessBoard : IReadOnlyChessBoard
    {
        readonly Piece[] board = new Piece[(int)Square.NB];
        readonly Bitboard[] colorBitboards = new Bitboard[(int)Color.NB];
        readonly Bitboard[] typeBitboards = new Bitboard[(int)PieceType.NB];
        readonly Bitboard[] pieceBitboards = new Bitboard[(int)Piece.NB];
        readonly int[] pieceCounts = new int[(int)Piece.NB];

        Score materialScore;

        public Piece PieceAt(Square s)
        {
            return board[(int)s];
        }
       
        public Square KingSquare(Color c)
        {
            return (colorBitboards[(int)c] & typeBitboards[(int)King]).LeastSignificantSquare();
        }
       
        public Bitboard Pieces()
        {
            return colorBitboards[(int)White] | typeBitboards[(int)Black];
        }
       
        public Bitboard Pieces(Color c)
        {
            return colorBitboards[(int)c];
        }
        
        public Bitboard Pieces(PieceType pt)
        {
            return typeBitboards[(int)pt];
        }
        
        public Bitboard Pieces(PieceType pt1, PieceType pt2)
        {
            return (typeBitboards[(int)pt1] | typeBitboards[(int)pt2]);
        }
        
        public Bitboard Pieces(Color c, PieceType pt)
        {
            return colorBitboards[(int)c] & typeBitboards[(int)pt];
        }
        
        public Bitboard Pieces(Color c, PieceType pt1, PieceType pt2)
        {
            return colorBitboards[(int)c] & (typeBitboards[(int)pt1] | typeBitboards[(int)pt2]);
        }

        public void SetPieceAt(Piece p, Square s)
        {
            Debug.Assert(IsValid(s) && PieceAt(s) == Piece.None);

            Bitboard b = s.ToBitboard();

            board[(int)s] = p;
            typeBitboards[(int)CoreFunctions.GetPieceType(p)] ^= b;
            colorBitboards[(int)p.GetColor()] ^= b;
            pieceBitboards[(int)p] ^= b;
            pieceCounts[(int)p]++;
            materialScore += PSQT.Get(p, s);
        }
        
        public void RemovePieceAt(Square s)
        {
            Debug.Assert(IsValid(s) && PieceAt(s) != Piece.None);

            Piece p = PieceAt(s);
            Bitboard b = s.ToBitboard();

            board[(int)s] = Piece.None;
            typeBitboards[(int)CoreFunctions.GetPieceType(p)] ^= b;
            colorBitboards[(int)p.GetColor()] ^= b;
            pieceBitboards[(int)p] ^= b;
            pieceCounts[(int)p]--;
            materialScore -= PSQT.Get(p, s);
        }
        
        public void MovePiece(Square from, Square to)
        {
            Debug.Assert(IsValid(from) && IsValid(to) && PieceAt(from) != Piece.None && from != to);

            Piece p = PieceAt(from);
            Bitboard fromto = from.ToBitboard() | to.ToBitboard();
            board[(int)from] = Piece.None;
            board[(int)to] = p;
            typeBitboards[(int)CoreFunctions.GetPieceType(p)] ^= fromto;
            colorBitboards[(int)p.GetColor()] ^= fromto;
            pieceBitboards[(int)p] ^= fromto;
            materialScore += PSQT.Get(p, to) - PSQT.Get(p, from);
        }

        public void Clear()
        {
            Array.Clear(board);
            Array.Clear(typeBitboards);
            Array.Clear(colorBitboards);
            Array.Clear(pieceCounts);
        }
    }

    public interface IReadOnlyChessBoard
    {
        Piece PieceAt(Square s);
        Square KingSquare(Color c);
        Bitboard Pieces();
        Bitboard Pieces(Color c);
        Bitboard Pieces(PieceType pt);
        Bitboard Pieces(PieceType pt1, PieceType pt2);
        Bitboard Pieces(Color c, PieceType pt);
        Bitboard Pieces(Color c, PieceType pt1, PieceType pt2);
    }
}
