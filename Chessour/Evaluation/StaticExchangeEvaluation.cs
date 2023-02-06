using static Chessour.Bitboards;
using static Chessour.Evaluation.ValueConstants;
using static Chessour.PieceType;

namespace Chessour.Evaluation;

internal static class StaticExchangeEvaluation
{
    public static bool SeeGe(this Position position, Move m, Value threshold = 0)
    {
        if (m.Type != MoveType.Quiet)
            return 0 >= threshold;

        Square from = m.From;
        Square to = m.To;

        int swap = Pieces.PieceScore(position.PieceAt(to)).MidGame - threshold;

        if (swap < 0)
            return false;

        swap = Pieces.PieceScore(position.PieceAt(from)).MidGame - swap;
        if (swap <= 0)
            return true;


        Color side = position.ActiveColor;
        Bitboard occupied = position.Pieces() ^ from.ToBitboard() ^ to.ToBitboard();
        Bitboard attackers = position.AttackersTo(to, occupied);
        Bitboard sideAttackers, bb;
        int result = 1;

        while (true)
        {
            side = side.Flip();
            attackers &= occupied;

            if ((sideAttackers = attackers & position.Pieces(side)) == 0)
                break;


            if ((position.Pinners(side.Flip()) & occupied) != 0)
            {
                sideAttackers &= ~position.BlockersForKing(side);

                if (sideAttackers == 0)
                    break;
            }

            result ^= 1;

            if ((bb = sideAttackers & position.Pieces(Pawn)) != 0)
            {
                if ((swap = PawnMGValue - swap) < result)
                    break;

                occupied ^= bb.LeastSignificantBit();
                attackers |= BishopAttacks(to, occupied) & position.Pieces(Bishop, Queen);
            }
            else if ((bb = sideAttackers & position.Pieces(Knight)) != 0)
            {
                if ((swap = KnightMGValue - swap) < result)
                    break;

                occupied ^= bb.LeastSignificantBit();
            }
            else if ((bb = sideAttackers & position.Pieces(Bishop)) != 0)
            {
                if ((swap = BishopMGValue - swap) < result)
                    break;

                occupied ^= bb.LeastSignificantBit();
                attackers |= BishopAttacks(to, occupied) & position.Pieces(Bishop, Queen);
            }
            else if ((bb = sideAttackers & position.Pieces(Rook)) != 0)
            {
                if ((swap = RookMGValue - swap) < result)
                    break;

                occupied ^= bb.LeastSignificantBit();
                attackers |= RookAttacks(to, occupied) & position.Pieces(Rook, Queen);
            }
            else if ((bb = sideAttackers & position.Pieces(Queen)) != 0)
            {
                if ((swap = QueenMGValue - swap) < result)
                    break;

                occupied ^= bb.LeastSignificantBit();
                attackers |= BishopAttacks(to, occupied) & position.Pieces(Bishop, Queen)
                          | RookAttacks(to, occupied) & position.Pieces(Rook, Queen);
            }
            else
            {
                result ^= (attackers & ~position.Pieces(side)) != 0 ? 1 : 0;
                break;
            }
        }
        return result > 0;
    }
}