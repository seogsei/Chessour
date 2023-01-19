﻿using System.Text;
using static Chessour.Bitboards;
using static Chessour.Types.PieceType;

namespace Chessour
{
    public enum Value
    {
        Zero = 0,
        Draw = 0,
        KnownWin = MateInMaxPly - 1,
        MateInMaxPly = Mate - SearchObject.MaxPly,
        Mate = 32000,
        Max = 32001,

        KnownLoss = -KnownWin,
        Mated = -Mate,
        Min = -Max,

        PawnMG = 100, PawnEG = 110,
        KnightMG = 300, KnightEG = 300,
        BishopMG = 300, BishopEG = 300,
        RookMG = 440, RookEG = 480,
        QueenMG = 890, QueenEG = 950,
    }

    public readonly struct Score
    {
        readonly short midGame;
        readonly short endGame;

        public Value MidGame { get => (Value)midGame; }
        public Value EndGame { get => (Value)endGame; }

        public Score(Value midGame, Value endGame)
        {
            this.midGame = (short)midGame;
            this.endGame = (short)endGame;
        }
        public Score(int midGame, int endGame)
        {
            this.midGame = (short)midGame;
            this.endGame = (short)endGame;
        }

        public static Score Zero { get; } = default;

        public static Score operator +(Score lhs, Score rhs)
        {
            return new Score(lhs.midGame + rhs.midGame, lhs.endGame + rhs.endGame);
        }
        public static Score operator -(Score lhs, Score rhs)
        {
            return new Score(lhs.midGame - rhs.midGame, lhs.endGame - rhs.endGame);
        }
        public static Score operator -(Score score)
        {
            return new Score(-score.midGame, -score.endGame);
        }
    }

    public static class Evaluation
    {
        public static Value Negate(this Value value)
        {
            return (Value)(-(int)value);
        }
        static Score S(int mg, int eg) => new(mg, eg);

        internal static class Trace
        {
            public enum Term
            {
                Material,
                Mobility,
                Total,
                NB,
            }

            readonly static Score[,] scores = new Score[(int)Term.NB, (int)Color.NB];

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

        public static void Init() { }

        static readonly Value[][] pieceValues = new Value[(int)GamePhase.NB][]
        {
            new Value[(int)Piece.NB]
            {
                Value.Zero, Value.PawnMG, Value.KnightMG, Value.BishopMG, Value.RookMG, Value.QueenMG, Value.Zero, Value.Zero,
                Value.Zero, Value.PawnMG, Value.KnightMG, Value.BishopMG, Value.RookMG, Value.QueenMG, Value.Zero

            },
            new Value[(int)Piece.NB]
            {
                Value.Zero, Value.PawnEG, Value.KnightEG, Value.BishopEG, Value.RookEG, Value.QueenEG, Value.Zero, Value.Zero,
                Value.Zero, Value.PawnEG, Value.KnightEG, Value.BishopEG, Value.RookEG, Value.QueenEG, Value.Zero
            }
        };
        static readonly Score[][] pieceMobility = new Score[(int)PieceType.NB][]
        {
            Array.Empty<Score>(),
            Array.Empty<Score>(),
            new Score[9] {S(-70,-80), //Knight
                            S(-53,-55), S(-12,-10), S(-2,-17), S(3,5), S(10,15),
                            S(25,20), S(38,25), S(45,30) },
            new Score[14] {S(-45,-59), //Bishop
                            S(-20,-25), S(14,-8), S(29,5), S(40,20), S(50,42),
                            S(60,58), S(62,65), S(68,72), S(75,78), S(83,78),
                            S(90,88), S(95,90), S(100,100) },
            new Score[15] {S(-60,-82), //Rook
                            S(-25,-15), S(1,17), S(3,42), S(5,73), S(15,95),
                            S(20,108), S(30,110), S(42,131), S(42,142), S(42,145),
                            S(46,155), S(50,160), S(59,165), S(68,170) },
            new Score[28] {S(-29,-49), //Queen
                            S(-16,-29), S( -8, -8), S( -8, 17), S( 18, 39), S( 25, 54),
                            S( 23, 59), S( 37, 73), S( 41, 76), S( 54, 95), S( 65, 95),
                            S( 68,101), S( 69,124), S( 70,128), S( 70,132), S( 70,133),
                            S( 71,136), S( 72,140), S( 74,147), S( 76,149), S( 90,153),
                            S(104,169), S(105,171), S(106,171), S(112,178), S(114,185),
                            S(114,187), S(119,221)},
            new Score[9] {S(0,0), //King
                            S(0,0), S(0,0), S(0,0), S(0,0), S(0,0),
                            S(0,0), S(0,0), S(0,0)  },
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
                side = side.Opposite();
                attackers &= occupied;

                if ((sideAttackers = attackers & position.Pieces(side)) == 0)
                    break;


                if ((position.Pinners(side.Opposite()) & occupied) != 0)
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
    }
}
