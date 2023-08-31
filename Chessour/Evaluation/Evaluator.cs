using Chessour.Search;
using System.Text;
using static Chessour.Bitboards;
using static Chessour.Color;

namespace Chessour.Evaluation
{
    public class Evaluator
    {
        public const int DrawValue = 0;

        public const int ExpectedWin = 25000;
        public const int ExpectedLoss = -ExpectedWin;

        public const int MateValue = 32000;
        public const int Infinite = 32001;

        public const int MateInMaxPly = MateValue - DepthConstants.MAX_PLY;
        public const int MatedInMaxPly = -MateInMaxPly;

        public const int Tempo = 15;

        public static int MatedIn(int ply)
        {
            return -MateValue + ply;
        }

        public static int MateIn(int ply)
        {
            return MateValue - ply;
        }

        public static double ToCentiPawn(int score)
        {
            return (double)score / Pieces.Pawn.EndGame;
        }

        public static int Evaluate(Position position, Trace? trace = null)
        {
            Debug.Assert(!position.IsCheck());

            Color us = position.ActiveColor;
            Color enemy = us.Flip();

            ScoreTuple score = Material(position, trace);

            score += PieceMobility(us, position, trace) - PieceMobility(enemy, position, trace);

            return TaperedEval(score, position, trace) + Tempo;
        }

        private static ScoreTuple Material(Position position, Trace? trace)
        {
            ScoreTuple score = position.ActiveColor == White ? position.PSQScore : -position.PSQScore;

            trace?.Set(Trace.Term.Material, White, position.PSQScore);

            return score;
        }

        private static ScoreTuple PieceMobility(Color side, Position pos, Trace? trace)
        {
            ScoreTuple score = ScoreTuple.Zero;

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

        private static int TaperedEval(ScoreTuple score, Position position, Trace? trace)
        {
            int midGame = score.MidGame;
            int endGame = score.EndGame;

            trace?.Set(Trace.Term.Total, White, score);

            int phase = position.PhaseValue;

            int normalizedPhase = phase * 256 / Phase.Total;

            int eval = (endGame * (256 - normalizedPhase) + midGame * normalizedPhase) / 256;

            return eval;
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

            private readonly ScoreTuple[,] scores = new ScoreTuple[(int)Term.NB, (int)NB];

            public void Set(Term term, Color side, ScoreTuple score)
            {
                scores[(int)term, (int)side] = score;
            }

            private string ToString(ScoreTuple score)
            {
                return string.Format("{0,5:N2} {1,5:N2}", ToCentiPawn(score.MidGame), ToCentiPawn(score.EndGame));
            }

            private string ToString(Term term)
            {
                string str;

                if (term == Term.Material || term == Term.Total)
                    str = string.Join(" | ", "----- -----", "----- -----");
                else
                    str = string.Join(" | ", ToString(scores[(int)term, (int)White]), ToString(scores[(int)term, (int)Black]));

                return string.Join(" | ", str, ToString(scores[(int)term, (int)White] - scores[(int)term, (int)Black]));
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