using Chessour.Search;
using System.Text;
using static Chessour.Bitboards;
using static Chessour.Color;

namespace Chessour
{
    internal static class Evaluation
    {
        public const int Draw = 0;

        public const int ExpectedWin = 10000;
        public const int ExpectedLoss = -ExpectedWin;

        public const int Mate = 32000;
        public const int Mated = -32000;
        public const int Infinite = 32001;

        public const int MateInMaxPly = Mate - DepthConstants.MAX_PLY;
        public const int MatedInMaxPly = -MateInMaxPly;

        public const int Tempo = 13;

        public static int MatedIn(int ply)
        {
            return -Mate + ply;
        }

        public static int MateIn(int ply)
        {
            return Mate - ply;
        }

        public static double ToCentiPawn(int score)
        {
            return (double)score / Pieces.PawnEndGame;
        }

        public static bool StaticExchangeEvaluationGE(this Position position, Move move, int threshold = 0)
        {
            if (move.Type() != MoveType.Quiet)
                return 0 >= threshold;

            Square from = move.DestinationSquare();
            Square to = move.OriginSquare();

            int swap = Pieces.PieceValue(position.PieceAt(to)) - threshold;

            if (swap < 0)
                return false;

            swap = Pieces.PieceValue(position.PieceAt(from)) - swap;
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
                    attackers |= BishopAttacks(to, occupied) & position.Pieces(PieceType.Bishop, PieceType.Queen)
                              | RookAttacks(to, occupied) & position.Pieces(PieceType.Rook, PieceType.Queen);
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

            ScoreExt score = Material(position, trace);

            score += PieceMobility(White, position, trace) - PieceMobility(Black, position, trace);

            int result = TaperedEval(score, position, trace);
            return position.ActiveColor == White ? result : -result;
        }

        private static ScoreExt Material(Position position, Trace? trace)
        {
            ScoreExt score = position.PSQScore;

            trace?.Set(Trace.Term.Material, White, position.PSQScore);

            return score;
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

            phase = (Phase)(((int)phase * 256 + (int)Phase.Total / 2) / (int)Phase.Total);

            int result = (eg * (256 - (int)phase) + mg * (int)phase) / 256;

            trace?.Set(Trace.Term.Total, Color.White, score);

            return result;
        }

        internal static class Pieces
        {
            public const int PawnMidGame = 100;
            public const int PawnEndGame = 135;
            public const int KnightMidGame = 305;
            public const int KnightEndGame = 290;
            public const int BishopMidGame = 333;
            public const int BishopEndGame = 333;
            public const int RookMidGame = 563;
            public const int RookEndGame = 600;
            public const int QueenMidGame = 950;
            public const int QueenEndGame = 1070;

            private static readonly ScoreExt[][] pieceMobility = new ScoreExt[(int)PieceType.NB][]
            {
                Array.Empty<ScoreExt>(),
                Array.Empty<ScoreExt>(),
                new ScoreExt[9] {new(-70, -80), //Knight
                                new(-53, -55), new(-12, -10), new(-2, -17), new(3, 5), new(10, 15),
                                new(25, 20), new(38, 25), new(45, 30) },
                new ScoreExt[14] {new(-45, -59), //Bishop
                                new(-20, -25), new(14, -8), new(29, 5), new(40, 20), new(50, 42),
                                new(60, 58), new(62, 65), new(68, 72), new(75, 78), new(83, 78),
                                new(90, 88), new(95, 90), new(100, 100) },
                new ScoreExt[15] {new(-60, -82), //Rook
                                new(-25, -15), new(1, 17), new(3, 42), new(5, 73), new(15, 95),
                                new(20, 108), new(30, 110), new(42, 131), new(42, 142), new(42, 145),
                                new(46, 155), new(50, 160), new(59, 165), new(68, 170) },
                new ScoreExt[28] {new(-29, -49), //Queen
                                new(-16, -29), new(-8, -8), new(-8, 17), new(18, 39), new(25, 54),
                                new(23, 59), new(37, 73), new(41, 76), new(54, 95), new(65, 95),
                                new(68, 101), new(69, 124), new(70, 128), new(70, 132), new(70, 133),
                                new(71, 136), new(72, 140), new(74, 147), new(76, 149), new(90, 153),
                                new(104, 169), new(105, 171), new(106, 171), new(112, 178), new(114, 185),
                                new(114, 187), new(119, 221)},
                Array.Empty<ScoreExt>(),
            };

            private static readonly ScoreExt[] pieceScores = new ScoreExt[(int)PieceType.NB]
            {
                ScoreExt.Zero,
                new (PawnMidGame, PawnEndGame),
                new (KnightMidGame, KnightEndGame),
                new (BishopMidGame, BishopEndGame),
                new (RookMidGame, RookEndGame),
                new (QueenMidGame, QueenEndGame),
                ScoreExt.Zero,
            };

            private static readonly int[] pieceValues = new int[(int)PieceType.NB]
            {
                0,
                PawnMidGame,
                KnightMidGame,
                BishopMidGame,
                RookMidGame,
                QueenMidGame,
                0
            };

            public static ScoreExt PieceScore(Piece piece)
            {
                return PieceScore(piece.TypeOf());
            }

            public static ScoreExt PieceScore(PieceType pieceType)
            {
                return pieceScores[(int)pieceType];
            }

            public static int PieceValue(Piece piece)
            {
                return PieceValue(piece.TypeOf());
            }

            public static int PieceValue(PieceType pieceType)
            {
                return pieceValues[(int)pieceType];
            }

            public static ScoreExt PieceMobility(PieceType pieceType, int attackableSquares)
            {
                return pieceMobility[(int)pieceType][attackableSquares];
            }
        }

        internal static class Pawns
        {
            private static Bitboard[][] pawnFrontSpan = new Bitboard[(int)Color.NB][];

            static Pawns()
            {
                pawnFrontSpan[(int)Color.White] = new Bitboard[(int)Square.NB];


                for (Square square = Square.a2; square <= Square.h7; square++)
                {

                }
            }

            public static ScoreExt Evaluate(Color side, Position position, Trace? trace)
            {
                Color enemy = side.Flip();

                Bitboard ourPawns = position.Pieces(side, PieceType.Pawn);
                Bitboard enemyPawns = position.Pieces(enemy, PieceType.Pawn);

                foreach (Square pawnSquare in ourPawns)
                {

                }

                return ScoreExt.Zero;
            }
        }

        internal static class PSQT
        {
            static PSQT()
            {
                Span<Piece> whitePieces = stackalloc Piece[]
                {
                    Piece.WhitePawn,
                    Piece.WhiteKnight,
                    Piece.WhiteBishop,
                    Piece.WhiteRook,
                    Piece.WhiteQueen,
                    Piece.WhiteKing
                };

                foreach (Piece whitePiece in whitePieces)
                {
                    ScoreExt pieceScore = Evaluation.Pieces.PieceScore(whitePiece);
                    Piece blackPiece = whitePiece.Flip();

                    psqt[(int)whitePiece] = new ScoreExt[(int)Square.NB];
                    psqt[(int)blackPiece] = new ScoreExt[(int)Square.NB];

                    for (Square s = Square.a1; s < Square.NB; s++)
                    {
                        psqt[(int)whitePiece][(int)s] = pieceScore + (whitePiece.TypeOf() == PieceType.Pawn ? bonuses[(int)whitePiece][(int)s.GetRank(), (int)s.GetFile()]
                                                                                                            : bonuses[(int)whitePiece][(int)s.GetRank(), s.GetFile().EdgeDistance()]); ;

                        psqt[(int)blackPiece][(int)s.FlipRank()] = -psqt[(int)whitePiece][(int)s];
                    }
                }
            }

            private static readonly ScoreExt[,] pawnBonuses = new ScoreExt[(int)Rank.NB, (int)File.NB]
            {
                { new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0) },
                { new(  2, -8), new(  4, -6), new( 11,  9), new( 18,  5), new( 16, 16), new( 21,  6), new(  9, -6), new( -3,-18) },
                { new( -9, -9), new(-15, -7), new( 11,-10), new( 15,  5), new( 31,  2), new( 23,  3), new(  6, -8), new(-20, -5) },
                { new( -3,  7), new(-20,  1), new(  8, -8), new( 19, -2), new( 39,-14), new( 17,-13), new(  2,-11), new( -5, -6) },
                { new( 11, 12), new( -4,  6), new(-11,  2), new(  2, -6), new( 11, -5), new(  0, -4), new(-12, 14), new(  5,  9) },
                { new(  3, 27), new(-11, 18), new( -6, 19), new( 22, 29), new( -8, 30), new( -5,  9), new(-14,  8), new(-11, 14) },
                { new( -7, -1), new(  6,-14), new( -2, 13), new(-11, 22), new(  4, 24), new(-14, 17), new( 10,  7), new( -9,  7) },
                { new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0), new(  0,  0) },
            };
            private static readonly ScoreExt[,] knightBonuses = new ScoreExt[(int)Rank.NB, (int)File.NB / 2]
            {
                { new(-175, -96), new(-92,-65), new(-74,-49), new(-73,-21) },
                { new( -77, -67), new(-41,-54), new(-27,-18), new(-15,  8) },
                { new( -61, -40), new(-17,-27), new(  6, -8), new( 12, 29) },
                { new( -35, -35), new(  8, -2), new( 40, 13), new( 49, 28) },
                { new( -34, -45), new( 13,-16), new( 44,  9), new( 51, 39) },
                { new(  -9, -51), new( 22,-44), new( 58,-16), new( 53, 17) },
                { new( -67, -69), new(-27,-50), new(  4,-51), new( 37, 12) },
                { new(-201,-100), new(-83,-88), new(-56,-56), new(-26,-17) },
            };
            private static readonly ScoreExt[,] bishopBonuses = new ScoreExt[(int)Rank.NB, (int)File.NB / 2]
            {
                { new(-37,-40), new(-4 ,-21), new( -6,-26), new(-16, -8) },
                { new(-11,-26), new(  6, -9), new( 13,-12), new(  3,  1) },
                { new(-5 ,-11), new( 15, -1), new( -4, -1), new( 12,  7) },
                { new(-4 ,-14), new(  8, -4), new( 18,  0), new( 27, 12) },
                { new(-8 ,-12), new( 20, -1), new( 15,-10), new( 22, 11) },
                { new(-11,-21), new(  4,  4), new(  1,  3), new(  8,  4) },
                { new(-12,-22), new(-10,-14), new(  4, -1), new(  0,  1) },
                { new(-34,-32), new(  1,-29), new(-10,-26), new(-16,-17) },
            };
            private static readonly ScoreExt[,] rookBonuses = new ScoreExt[(int)Rank.NB, (int)File.NB / 2]
            {
                { new(-31, -9), new(-20,-13), new(-14,-10), new(-5, -9) },
                { new(-21,-12), new(-13, -9), new( -8, -1), new( 6, -2) },
                { new(-25,  6), new(-11, -8), new( -1, -2), new( 3, -6) },
                { new(-13, -6), new( -5,  1), new( -4, -9), new(-6,  7) },
                { new(-27, -5), new(-15,  8), new( -4,  7), new( 3, -6) },
                { new(-22,  6), new( -2,  1), new(  6, -7), new(12, 10) },
                { new( -2,  4), new( 12,  5), new( 16, 20), new(18, -5) },
                { new(-17, 18), new(-19,  0), new( -1, 19), new( 9, 13) },
            };
            private static readonly ScoreExt[,] queenBonuses = new ScoreExt[(int)Rank.NB, (int)File.NB / 2]
            {
                { new( 3,-69), new(-5,-57), new(-5,-47), new( 4,-26) },
                { new(-3,-54), new( 5,-31), new( 8,-22), new(12, -4) },
                { new(-3,-39), new( 6,-18), new(13, -9), new( 7,  3) },
                { new( 4,-23), new( 5, -3), new( 9, 13), new( 8, 24) },
                { new( 0,-29), new(14, -6), new(12,  9), new( 5, 21) },
                { new(-4,-38), new(10,-18), new( 6,-11), new( 8,  1) },
                { new(-5,-50), new( 6,-27), new(10,-24), new( 8, -8) },
                { new(-2,-74), new(-2,-52), new( 1,-43), new(-2,-34) },
            };
            private static readonly ScoreExt[,] kingBonuses = new ScoreExt[(int)Rank.NB, (int)File.NB / 2]
            {
                { new(270, 10), new(330, 40), new(270, 80), new(200, 80) },
                { new(270, 40), new(300,100), new(230,110), new(170,130) },
                { new(190, 90), new(258,140), new(196,165), new( 12,170) },
                { new(164,100), new(190,160), new(152,170), new( 50,175) },
                { new(154,100), new(170,160), new(100,200), new( 48,200) },
                { new(123, 90), new(142,170), new( 84,200), new( 55,200) },
                { new( 80, 40), new(120,110), new( 60,110), new( 39,130) },
                { new( 59, 11), new( 80, 60), new( 40, 75), new(  0, 80) },
            };

            private static readonly ScoreExt[][,] bonuses = new ScoreExt[(int)PieceType.NB][,]
            {
                new ScoreExt[0,0],
                pawnBonuses,
                knightBonuses,
                bishopBonuses,
                rookBonuses,
                queenBonuses,
                kingBonuses
            };

            private static readonly ScoreExt[][] psqt = new ScoreExt[(int)Piece.NB][];

            public static ScoreExt Get(Piece piece, Square square)
            {
                return psqt[(int)piece][(int)square];
            }
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
                return string.Format("{0,5:N2} {1,5:N2}", ToCentiPawn(score.MidGame), ToCentiPawn(score.EndGame));
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