using System.Text;
using static Chessour.Bitboards;
using static Chessour.Types.PieceType;

namespace Chessour
{
    internal static class Evaluation
    {
        private static readonly Value[][] pieceValues = new Value[(int)GamePhase.NB][]
        {
            new Value[(int)Piece.NB]
            {
                Value.Zero, Value.PawnMG, Value.KnightMG, Value.BishopMG, Value.RookMG, Value.QueenMG, Value.Zero, Value.Zero,
                Value.Zero, Value.PawnMG, Value.KnightMG, Value.BishopMG, Value.RookMG, Value.QueenMG, Value.Zero, Value.Zero

            },
            new Value[(int)Piece.NB]
            {
                Value.Zero, Value.PawnEG, Value.KnightEG, Value.BishopEG, Value.RookEG, Value.QueenEG, Value.Zero, Value.Zero,
                Value.Zero, Value.PawnEG, Value.KnightEG, Value.BishopEG, Value.RookEG, Value.QueenEG, Value.Zero, Value.Zero
            }
        };
        private static readonly Score[][] pieceMobility = new Score[(int)PieceType.NB][]
        {
            Array.Empty<Score>(),
            Array.Empty<Score>(),
            new Score[9] {new(-70, -80), //Knight
                            new(-53, -55), new(-12, -10), new(-2, -17), new(3, 5), new(10, 15),
                            new(25, 20), new(38, 25), new(45, 30) },
            new Score[14] {new(-45, -59), //Bishop
                            new(-20, -25), new(14, -8), new(29, 5), new(40, 20), new(50, 42),
                            new(60, 58), new(62, 65), new(68, 72), new(75, 78), new(83, 78),
                            new(90, 88), new(95, 90), new(100, 100) },
            new Score[15] {new(-60, -82), //Rook
                            new(-25, -15), new(1, 17), new(3, 42), new(5, 73), new(15, 95),
                            new(20, 108), new(30, 110), new(42, 131), new(42, 142), new(42, 145),
                            new(46, 155), new(50, 160), new(59, 165), new(68, 170) },
            new Score[28] {new(-29, -49), //Queen
                            new(-16, -29), new(-8, -8), new(-8, 17), new(18, 39), new(25, 54),
                            new(23, 59), new(37, 73), new(41, 76), new(54, 95), new(65, 95),
                            new(68, 101), new(69, 124), new(70, 128), new(70, 132), new(70, 133),
                            new(71, 136), new(72, 140), new(74, 147), new(76, 149), new(90, 153),
                            new(104, 169), new(105, 171), new(106, 171), new(112, 178), new(114, 185),
                            new(114, 187), new(119, 221)},
            new Score[9] {new(0, 0), //King
                            new(0, 0), new(0, 0), new(0, 0), new(0, 0), new(0, 0),
                            new(0, 0), new(0, 0), new(0, 0)  },
        };
       
        public static Value PieceValue(GamePhase phase, Piece piece)
        {
            return pieceValues[(int)phase][(int)piece];
        }
       
        public static bool SeeGe(this Position position, Move m, Value threshold = 0)
        {
            if (m.TypeOf() != MoveType.Quiet)
                return 0 >= threshold;

            Square from = m.FromSquare();
            Square to = m.ToSquare();

            int swap = PieceValue(GamePhase.MidGame, position.PieceAt(to)) - threshold;
            if (swap < 0)
                return false;

            swap = (int)PieceValue(GamePhase.MidGame, position.PieceAt(from)) - swap;
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
                    if ((swap = (int)Value.PawnMG - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                    attackers |= Attacks(Bishop, to, occupied) & position.Pieces(Bishop, Queen);
                }
                else if ((bb = sideAttackers & position.Pieces(Knight)) != 0)
                {
                    if ((swap = (int)Value.KnightMG - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                }
                else if ((bb = sideAttackers & position.Pieces(Bishop)) != 0)
                {
                    if ((swap = (int)Value.BishopMG - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                    attackers |= Attacks(Bishop, to, occupied) & position.Pieces(Bishop, Queen);
                }
                else if ((bb = sideAttackers & position.Pieces(Rook)) != 0)
                {
                    if ((swap = (int)Value.RookMG - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                    attackers |= Attacks(Rook, to, occupied) & position.Pieces(Rook, Queen);
                }
                else if ((bb = sideAttackers & position.Pieces(Queen)) != 0)
                {
                    if ((swap = (int)Value.QueenMG - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                    attackers |= (Attacks(Bishop, to, occupied) & position.Pieces(Bishop, Queen))
                              | (Attacks(Rook, to, occupied) & position.Pieces(Rook, Queen));
                }
                else
                {
                    result ^= (attackers & ~position.Pieces(side)) != 0 ? 1 : 0;
                    break;
                }
            }
            return result > 0;
        }

        public static double ToPawnValue(Value evaluation)
        {
            return (int)evaluation / (double)Value.PawnMG;
        }
       
        public static Value Evaluate(Position position, bool trace = false)
        {
            //return new EvaluationContainer(position).Evaluate();
            if (position.IsCheck())
                return Value.Min;

            if (trace)
                Trace.Clear();

            Score score = position.PSQScore;

            score += PieceMobility(Color.White, position, trace) - PieceMobility(Color.Black, position, trace);

            if (trace)
            {
                Trace.Add(Trace.Term.Material, Color.White, position.PSQScore);
            }


            Value v = TaperedEval(score, position, trace);
            return position.ActiveColor == Color.White ? v : v.Negate();
        }

        private static Value TaperedEval(Score score, Position position, bool trace)
        {
            Value mg = score.MidGame;
            Value eg = score.EndGame;

            PhaseValues phase = PhaseValues.Total;

            phase -= (position.PieceCount(MakePiece(Color.White, Pawn)) + position.PieceCount(MakePiece(Color.Black, Pawn))) * (int)PhaseValues.Pawn;
            phase -= (position.PieceCount(MakePiece(Color.White, Knight)) + position.PieceCount(MakePiece(Color.Black, Knight))) * (int)PhaseValues.Knight;
            phase -= (position.PieceCount(MakePiece(Color.White, Bishop)) + position.PieceCount(MakePiece(Color.Black, Bishop))) * (int)PhaseValues.Bishop;
            phase -= (position.PieceCount(MakePiece(Color.White, Rook)) + position.PieceCount(MakePiece(Color.Black, Rook))) * (int)PhaseValues.Rook;
            phase -= (position.PieceCount(MakePiece(Color.White, Queen)) + position.PieceCount(MakePiece(Color.Black, Queen))) * (int)PhaseValues.Queen;

            phase = (PhaseValues)(((int)phase * 256 + ((int)PhaseValues.Total / 2)) / (int)PhaseValues.Total);

            Value v = (Value)((((int)mg * (256 - (int)phase)) + ((int)eg * (int)phase)) / 256);

            if (trace)
                Trace.Add(Trace.Term.Total, Color.White, score);

            return v;
        }

        private static Score PieceMobility(Color side, Position pos, bool trace)
        {
            Score score = Score.Zero;

            Bitboard targets = ~pos.Pieces(side);
            Bitboard occupancy = pos.Pieces();
            Bitboard blockersForKing = pos.BlockersForKing(side);

            Square ksq = pos.KingSquare(side);

            for (PieceType pt = Knight; pt < King; pt++)
            {
                Bitboard pieces = pos.Pieces(side, pt);

                foreach (Square pieceSquare in pieces)
                {
                    //Piece mobility
                    Bitboard attacks = Attacks(pt, pieceSquare, occupancy) & targets;

                    if ((blockersForKing & pieceSquare.ToBitboard()) != 0)
                        attacks &= Line(ksq, pieceSquare);

                    score += pieceMobility[(int)pt][attacks.PopulationCount()];
                }
            }

            if (trace)
            {
                Trace.Add(Trace.Term.Mobility, side, score);
            }


            return score;
        }

        public static class Trace
        {
            public enum Term
            {
                Material,
                Mobility,
                Total,
                NB,
            }

            private static readonly Score[,] scores = new Score[(int)Term.NB, (int)Color.NB];

            public static void Clear()
            {
                Array.Clear(scores);
            }

            public static void Add(Term term, Color side, Score score)
            {
                scores[(int)term, (int)side] = score;
            }

            private static string ToString(Score score)
            {
                return string.Format("{0,5:N2} {1,5:N2}", ToPawnValue(score.MidGame), ToPawnValue(score.EndGame));
            }

            private static string ToString(Term term)
            {
                string str;

                if (term == Term.Material || term == Term.Total)
                    str = string.Join(" | ", "----- -----", "----- -----");
                else
                    str = string.Join(" | ", ToString(scores[(int)term, (int)Color.White]), ToString(scores[(int)term, (int)Color.Black]));

                return string.Join(" | ", str, ToString(scores[(int)term, (int)Color.White] - scores[(int)term, (int)Color.Black]));
            }

            public static new string ToString()
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
