using Chessour.Evaluation;
using Chessour.Search;
using Chessour.Utilities;
using System.Collections.Generic;
using System.Text;

namespace Chessour
{
    public static class UCI
    {
        public const string StartFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public static void Loop(string[] args)
        {
            Position position = new(StartFEN, new());

            string command;
            do
            {
                StringStream ss = args.Length == 0 ? new(Console.ReadLine() ?? "quit")
                                                   : new(string.Join(' ', args));

                ss.SkipWhiteSpace();
                ss.Extract(out command);

                switch (command)
                {
                    case "quit" or "stop":
                        Engine.Stop = true;
                        break;
                    case "ponderhit":
                        Engine.PonderMode = false;
                        break;
                    case "uci":
                        Identify();
                        break;
                    case "setoption":
                        SetOption(ref ss);
                        break;
                    case "isready":
                        Console.WriteLine("readyok");
                        break;
                    case "ucinewgame":
                        Engine.NewGame();
                        break;
                    case "position":
                        Position(ref ss, position);
                        break;
                    case "go":
                        Go(ref ss, position);
                        break;
                    case "bench":
                        Bench(ref ss, position);
                        break;
                    case "d":
                        Console.WriteLine(position);
                        break;
                    case "eval":
                        ShowEvaluation(position);
                        break;

                    default:
                        Console.WriteLine($"Unrecognized command: {command}");
                        break;
                }

            } while (command != "quit" && args.Length == 0);
        }

        private static void Position(ref StringStream ss, Position position)
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
                if (m != Chessour.Move.None)
                    position.MakeMove(m, new Position.StateInfo());
                else
                    return;
            }
        }

        private static void Go(ref StringStream ss, Position position)
        {
            GoParameters limits = new()
            {
                startTime = TimeManager.Now()
            };

            bool ponder = false;

            while (ss.Extract(out string token))
                switch (token)
                {
                    case "searchmoves":
                        limits.moves = new();
                        while (ss.Extract(out token))
                            limits.moves.Add(ParseMove(position, token));
                        break;
                    case "ponder":
                        ponder = true;
                        break;
                    case "wtime":
                        limits.whiteTime = TimeSpan.FromMilliseconds(ss.ReadInt64());
                        break;
                    case "btime":
                        limits.blackTime = TimeSpan.FromMilliseconds(ss.ReadInt64());
                        break;
                    case "winc":
                        limits.whiteIncrement = TimeSpan.FromMilliseconds(ss.ReadInt64());
                        break;
                    case "binc":
                        limits.blackIncrement = TimeSpan.FromMilliseconds(ss.ReadInt64());
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

            Engine.StartThinking(position, limits, ponder);
        }

        private static void ShowEvaluation(Position position)
        {
            Evaluator.Trace trace = new();

            Evaluator.Evaluate(position, trace);

            Console.WriteLine(trace);
        }

        private static void Bench(ref StringStream ss, Position position)
        {
            Stopwatch timer = new();
            timer.Start();

            long nodes = 0;
            ss.Extract(out string token);

            if (token == "go")
            {
                Console.WriteLine($"\nPosition ({position.FEN()})");

                Go(ref ss, position);

                Engine.Threads.WaitForSearchFinish();
                nodes += Engine.Threads.TotalNodesSearched();
            }

            long elapsed = timer.ElapsedMilliseconds + 1;

            Console.WriteLine($"""

                Total time (ms) : {elapsed}
                Nodes searched : {nodes}
                Nps : {1000 * nodes / elapsed}
                """);
        }

        private static void SetOption(ref StringStream ss)
        {
            throw new NotImplementedException();
        }

        private static void Identify()
        {
            Console.WriteLine($"""
                id name {Engine.Name}
                id author {Engine.Author}
                uciok
                """);
        }

        internal static void SendPV(Searcher searcher, int depth)
        {
            StringBuilder sb = new(4 * 1024);

            var rootMove = searcher.rootMoves[0];
            var nodesSearched = Engine.Threads.TotalNodesSearched();
            var timeElapsed = Engine.Timer.Elapsed();

            sb.Append("info depth ").Append(depth);
            sb.Append(" seldepth ").Append(rootMove.SelectiveDepth);
            sb.Append(" score ").Append(Value(rootMove.UCIScore));
            sb.Append(" nodes ").Append(nodesSearched);
            sb.Append(" nps ").Append((long)(nodesSearched / timeElapsed.TotalSeconds));
            sb.Append(" hashfull ").Append(Engine.TranspositionTable.Hashfull());
            sb.Append(" time ").Append((long)timeElapsed.TotalMilliseconds);

            sb.Append(" pv");

            foreach (Move move in rootMove.PV)
                sb.Append(' ').Append(Move(move));

            Console.WriteLine(sb.ToString());
        }

        public static Move ParseMove(Position position, string str)
        {
            var moves = MoveGenerators.Legal.Generate(position, stackalloc MoveScore[256]);

            foreach (Move m in moves)
                if (str == Move(m))
                    return m;

            return Chessour.Move.None;
        }

        public static string Value(int value)
        {
            if (Math.Abs(value) > Evaluator.MateInMaxPly)
            {
                int mateDistance = (value > 0 ? Evaluator.MateScore - value + 1 : -Evaluator.MateScore - value) / 2;
                return "mate " + mateDistance;
            }
            else
                return "cp " + (value * 100 / Pieces.PawnValue);
        }

        public static string Move(Move move)
        {
            if (move == Chessour.Move.None)
                return "(none)";

            if (move == Chessour.Move.Null)
                return "0000";

            Square origin = move.Origin();
            Square destination = move.Destination();
            MoveType type = move.MoveType();

            if (type == MoveType.Castling)
                destination = SquareExtensions.MakeSquare(destination > origin ? File.g : File.c, origin.GetRank());

            string moveString = string.Concat(origin, destination);
            if (type == MoveType.Promotion)
                moveString += " pnbrqk"[(int)move.PromotionPiece()];

            return moveString;
        }

        internal class GoParameters
        {
            public int perft;

            public List<Move>? moves;

            public DateTime startTime;

            public TimeSpan whiteTime;
            public TimeSpan blackTime;
            public TimeSpan whiteIncrement;
            public TimeSpan blackIncrement;
            public long moveTime;
            public int movesToGo;

            public int mate;
            public int depth;
            public long nodes;

            public bool infinite;

            public bool RequiresTimeManagement()
            {
                return whiteTime != TimeSpan.Zero || whiteTime != TimeSpan.Zero;
            }
        }
    }
}
