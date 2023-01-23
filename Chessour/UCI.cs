using System.Collections.Generic;
using System.Text;
using Chessour.Utilities;

namespace Chessour
{
    static class UCI
    {
        public const string StartFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";     
     
        public struct SearchLimits
        {
            public List<Move>? searchMoves;
            public int whiteTime;
            public int blackTime;
            public int whiteIncrement;
            public int blackIncrement;
            public int movesToGo;
            public Depth depth;
            public int nodes;
            public int mate;
            public int moveTime;
            public bool infinite;
            
            public int perft;
        }

        public const int NormalizeToPawn = (int)Value.PawnMG;

        public static void Loop(string[] args)
        {
            Position position = new(StartFEN, new());

            string command;
            do
            {
                StringReader ss = args.Length == 0 ? new(Console.ReadLine() ?? "quit")
                                                    : new(string.Join(' ', args));

                ss.Extract(out command);

                switch (command)
                {
                    case "uci":
                        Console.WriteLine($"""
                            id name {Engine.Name}
                            id author {Engine.Author}
                            uciok
                            """);
                        break;
                    case "debug":
                        break;
                    case "isready":
                        Console.WriteLine("readyok");
                        break;
                    case "setoption":
                        break;
                    case "ucinewgame":
                        break;
                    case "position":
                        Position(ref ss, position);
                        break;
                    case "go":
                        Go(ref ss, position);
                        break;
                    case "quit":
                    case "stop":
                        Engine.Threads.Stop();
                        break;
                    case "ponderhit":
                        break;
                    case "bench":
                        Bench(ref ss, position);
                        break;
                    case "eval":
                        Eval(position);
                        break;
                    default:
                        Console.WriteLine($"Unrecognized command: {command}");
                        break;
                }

            } while (command != "quit" && args.Length == 0);
        }

        static void Position(ref StringReader ss, Position position)
        {
            ss.Extract(out string token);
            
            string fen;
            if (token == "startpos")
            {
                fen = StartFEN;
                ss.Extract(out string _);
            }
            else if (token == "fen")
                fen = ss.ReadUntil("moves");
            else
                return;

            position.Set(fen, new());

            //Handle Moves
            while (ss.Extract(out token))
            {
                Move m = ParseMove(position, token);
                if (m != Move.None)
                    position.MakeMove(m, new Position.StateInfo());
                else
                    return;
            }
        }

        static void Go(ref StringReader ss, Position position)
        {
            SearchLimits limits = new();
            bool ponder = false;

            while (ss.Extract(out string token))
                switch (token)
                {
                    case "searchmoves":
                        limits.searchMoves = new();
                        while (ss.Extract(out token))
                            limits.searchMoves.Add(ParseMove(position, token));
                        break;
                    case "ponder":
                        ponder = true;
                        break;
                    case "wtime":
                        ss.Extract(out limits.whiteTime);
                        break;
                    case "btime":
                        ss.Extract(out limits.blackTime);
                        break;
                    case "winc":
                        ss.Extract(out limits.whiteIncrement);
                        break;
                    case "binc":
                        ss.Extract(out limits.blackIncrement);
                        break;
                    case "movestogo":
                        ss.Extract(out limits.movesToGo);
                        break;
                    case "depth":
                        ss.Extract(out limits.depth);
                        break;
                    case "nodes":
                        ss.Extract(out limits.nodes);
                        break;
                    case "mate":
                        ss.Extract(out limits.mate);
                        break;
                    case "movetime":
                        ss.Extract(out limits.moveTime);
                        break;
                    case "infinite":
                        limits.infinite = true;
                        break;
                    case "perft":
                        ss.Extract(out limits.perft);
                        break;
                }

            Engine.Threads.StartThinking(position, in limits, ponder);
        }

        static void Bench(ref StringReader ss, Position position)
        {
            ulong nodes = 0;

            long elapsed = Engine.Now();
            ss.Extract(out string token);


            if (token == "go")
            {              
                Console.WriteLine($"\nPosition ({position.FEN()})");

                Go(ref ss, position);

                Engine.Threads.WaitForSeachFinish();
                nodes += Engine.Threads.NodesSearched();
            }


            elapsed = Engine.Now() - elapsed + 1;

            Console.WriteLine($"""

                Total time (ms) : {elapsed}
                Nodes searched : {nodes}
                Nps : {1000 * nodes / (ulong)elapsed}
                """);
        }
      
        static void Eval(Position position)
        {
            Evaluation.Evaluate(position, true);

            Console.WriteLine(Evaluation.Trace.ToString());
        }
    
        public static string ParsePV(Move[] pv)
        {
            StringBuilder sb = new(1024);

            int i = -1;
            while (pv[++i] != Move.None)
            {
                if (i != 0)
                    sb.Append(' ');
                sb.Append(pv[i].LongAlgebraicNotation());
            }

            return sb.ToString();
        }

        public static Move ParseMove(Position position, string str)
        {
            MoveList moveList = new(position, stackalloc MoveScore[MAX_MOVE_COUNT]);

            foreach (Move m in moveList)
                if (str == m.LongAlgebraicNotation())
                    return m;

            return Move.None;
        }

        public static string LongAlgebraicNotation(this Move move)
        {
            Square from = move.FromSquare();
            Square to = move.ToSquare();

            if (move == Move.None)
                return "(none)";

            if (move == Move.Null)
                return "0000";

            if (move.TypeOf() == MoveType.Castling)
                to = MakeSquare(to > from ? File.g : File.c, from.GetRank());

            string moveString = string.Concat(from, to);
            if (move.TypeOf() == MoveType.Promotion)
                moveString += " pnbrqk"[(int)move.PromotionPiece()];

            return moveString;
        }

        public static string ToString(Value v)
        {
            StringBuilder sb = new();

            if (Math.Abs((int)v) < (int)Value.MateInMaxPly)
            {
                sb.Append("cp ");
                sb.Append((int)v * 100 / NormalizeToPawn);
            }
            else
            {
                sb.Append("mate ");
                sb.Append((v > 0 ? Value.Mate - v + 1 : Value.Mated - v) / 2);
            }

            return sb.ToString();
        }
    }
}
