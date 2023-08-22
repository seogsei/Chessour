using Chessour.Search;
using System.Text;
using static Chessour.Bitboards;

namespace Chessour.Evaluation
{
    internal static class Evaluator
    {
        public const int Draw = 0;

        public const int ExpectedWin = 10000;
        public const int ExpectedLoss = -ExpectedWin;

        public const int Mate = 32000;
        public const int Mated = -32000;
        public const int Infinite = 32001;

        public const int MateInMaxPly = Mate - DepthConstants.MAX_PLY;
        public const int MatedInMaxPly = -MateInMaxPly;

        public static int MatedIn(int ply)
        {
            return -Mate + ply;
        }

        public static int MateIn(int ply)
        {
            return Mate - ply;
        }

        public static bool StaticExchangeEvaluationGE(this Position position, Move move, int threshold = 0)
        {
            if (move.Type() != MoveType.Quiet)
                return 0 >= threshold;

            Square from = move.DestinationSquare();
            Square to = move.OriginSquare();

            int swap = position.PieceAt(to).TypeOf().PieceScore().MidGame - threshold;

            if (swap < 0)
                return false;

            swap = position.PieceAt(from).TypeOf().PieceScore().MidGame - swap;
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

                if ((bb = sideAttackers & position.Pieces(PieceType.Pawn)) != 0)
                {
                    if ((swap = Pieces.PawnMidGame - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                    attackers |= BishopAttacks(to, occupied) & position.Pieces(PieceType.Bishop, PieceType.Queen);
                }
                else if ((bb = sideAttackers & position.Pieces(PieceType.Knight)) != 0)
                {
                    if ((swap = Pieces.KnightMidGame - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                }
                else if ((bb = sideAttackers & position.Pieces(PieceType.Bishop)) != 0)
                {
                    if ((swap = Pieces.BishopMidGame - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                    attackers |= BishopAttacks(to, occupied) & position.Pieces(PieceType.Bishop, PieceType.Queen);
                }
                else if ((bb = sideAttackers & position.Pieces(PieceType.Rook)) != 0)
                {
                    if ((swap = Pieces.RookMidGame - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                    attackers |= RookAttacks(to, occupied) & position.Pieces(PieceType.Rook, PieceType.Queen);
                }
                else if ((bb = sideAttackers & position.Pieces(PieceType.Queen)) != 0)
                {
                    if ((swap = Pieces.QueenMidGame - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                    attackers |= (BishopAttacks(to, occupied) & position.Pieces(PieceType.Bishop, PieceType.Queen))
                              | (RookAttacks(to, occupied) & position.Pieces(PieceType.Rook, PieceType.Queen));
                }
                else
                {
                    result ^= (attackers & ~position.Pieces(side)) != 0 ? 1 : 0;
                    break;
                }
            }
            return result > 0;
        }

        public static int Evaluate(Position position, Trace? trace = null)
        {
            Debug.Assert(!position.IsCheck());

            ScoreExt score = position.PSQScore;
            trace?.Set(Trace.Term.Material, Color.White, position.PSQScore);

            score += PieceMobility(Color.White, position, trace) - PieceMobility(Color.Black, position, trace);

            int result = TaperedEval(score, position, trace);
            return position.ActiveColor == Color.White ? result : -result;
        }

        private static ScoreExt PieceMobility(Color side, Position pos, Trace? trace)
        {
            ScoreExt score = ScoreExt.Zero;

            Bitboard targets = ~pos.Pieces(side);
            Bitboard occupancy = pos.Pieces();
            Bitboard blockersForKing = pos.BlockersForKing(side);

            Square ksq = pos.KingSquare(side);

            for (PieceType pt = PieceType.Knight; pt < PieceType.King; pt++)
            {
                Bitboard pieces = pos.Pieces(side, pt);

                foreach (Square pieceSquare in pieces)
                {
                    //Piece mobility
                    Bitboard attacks = Attacks(pt, pieceSquare, occupancy) & targets;
                        
                    if ((blockersForKing & pieceSquare.ToBitboard()) != 0)
                        attacks &= Line(ksq, pieceSquare);

                    score += Pieces.PieceMobility(pt, attacks.PopulationCount());
                }
            }

            trace?.Set(Trace.Term.Mobility, side, score);

            return score;
        }

        private static int TaperedEval(ScoreExt score, Position position, Trace? trace)
        {
            int mg = score.MidGame;
            int eg = score.EndGame;

            Phase phase = position.Phase;

            phase = (Phase)((((int)phase * 256) + ((int)Phase.Total / 2)) / (int)Phase.Total);

            int result = (((int)eg * (256 - (int)phase)) + ((int)mg * (int)phase)) / 256;
           
            trace?.Set(Trace.Term.Total, Color.White, score);

            return result;
        }

        public sealed class Trace
        {
            public enum Term
            {
                Material,
                Mobility,
                Total,
                NB,
            }

            private readonly ScoreExt[,] scores = new ScoreExt[(int)Term.NB, (int)Color.NB];

            public void Set(Term term, Color side, ScoreExt score)
            {
                scores[(int)term, (int)side] = score;
            }

            private string ToString(ScoreExt score)
            {
                return string.Format("{0,5:N2} {1,5:N2}", score.MidGame, score.EndGame);
            }

            private string ToString(Term term)
            {
                string str;

                if (term == Term.Material || term == Term.Total)
                    str = string.Join(" | ", "----- -----", "----- -----");
                else
                    str = string.Join(" | ", ToString(scores[(int)term, (int)Color.White]), ToString(scores[(int)term, (int)Color.Black]));

                return string.Join(" | ", str, ToString(scores[(int)term, (int)Color.White] - scores[(int)term, (int)Color.Black]));
            }

            public override string ToString()
            {
                StringBuilder sb = new();

                sb.AppendLine("+------------+-------------+-------------+-------------+");
                sb.AppendLine("|    Term    |    White    |    Black    |    Total    |");
                sb.AppendLine("|            |  MG     EG  |  MG     EG  |  MG     EG  |");
                sb.AppendLine("+------------+-------------+-------------+-------------+");
                sb.AppendLine("|   Material | " + ToString(Term.Material) + " |");
                sb.AppendLine("|   Mobility | " + ToString(Term.Mobility) + " |");
                sb.AppendLine("+------------+-------------+-------------+-------------+");
                sb.AppendLine("|      Total | " + ToString(Term.Total) + " |");
                sb.AppendLine("+------------+-------------+-------------+-------------+");

                return sb.ToString();
            }
        }
    }
}