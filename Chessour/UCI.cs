using Chessour.Evaluation;
using Chessour.Search;
using Chessour.Utilities;
using System.Collections.Generic;
using System.Text;
using static Chessour.Factory;

namespace Chessour
{
    public class UCI
    {
        internal const string StartFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public bool Debug { get; private set; }

        private readonly Position position;
        private readonly Position.StateInfo rootState;

        public UCI()
        {
            rootState = new();
            position = new(StartFEN, rootState);
        }

        public void Run(string[] args)
        {
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
                        Engine.Ponder = false;
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
                        Position(ref ss);
                        break;
                    case "go":
                        Go(ref ss);
                        break;
                    case "bench":
                        Bench(ref ss);
                        break;
                    case "d":
                        Console.WriteLine(position);
                        break;
                    case "eval":
                        ShowEvaluation();
                        break;

                    default:
                        Console.WriteLine($"Unrecognized command: {command}");
                        break;
                }

            } while (command != "quit" && args.Length == 0);
        }

        private void Position(ref StringStream ss)
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

        private void Go(ref StringStream ss)
        {
            GoParameters limits = new()
            {
                StartTime = TimeManager.Now()
            };

            bool ponder = false;

            while (ss.Extract(out string token))
                switch (token)
                {
                    case "searchmoves":
                        limits.Moves = new();
                        while (ss.Extract(out token))
                            limits.Moves.Add(ParseMove(position, token));
                        break;
                    case "ponder":
                        ponder = true;
                        break;
                    case "wtime":
                        ss.Extract(out limits.WhiteTime);
                        break;
                    case "btime":
                        ss.Extract(out limits.BlackTime);
                        break;
                    case "winc":
                        ss.Extract(out limits.WhiteIncrement);
                        break;
                    case "binc":
                        ss.Extract(out limits.BlackIncrement);
                        break;
                    case "movestogo":
                        ss.Extract(out limits.MovesToGo);
                        break;
                    case "depth":
                        ss.Extract(out limits.Depth);
                        break;
                    case "nodes":
                        ss.Extract(out limits.Nodes);
                        break;
                    case "mate":
                        ss.Extract(out limits.Mate);
                        break;
                    case "movetime":
                        ss.Extract(out limits.MoveTime);
                        break;
                    case "infinite":
                        limits.Infinite = true;
                        break;
                    case "perft":
                        ss.Extract(out limits.Perft);
                        break;
                }

            Engine.StartThinking(position, limits, ponder);
        }

        private void ShowEvaluation()
        {
            Evaluator.Trace trace = new();

            Evaluator.Evaluate(position, trace);

            Console.WriteLine(trace);
        }

        private void Bench(ref StringStream ss)
        {
            Stopwatch timer = new();
            timer.Start();

            long nodes = 0;
            ss.Extract(out string token);

            if (token == "go")
            {
                Console.WriteLine($"\nPosition ({position.FEN()})");

                Go(ref ss);

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

        private void SetOption(ref StringStream ss)
        {

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
            var timeElapsed = Engine.Timer.Elapsed() + 1;

            sb.Append("info depth ").Append(depth);
            sb.Append(" seldepth ").Append(rootMove.SelectiveDepth);
            sb.Append(" score ").Append(Value(rootMove.UCIScore));
            sb.Append(" nodes ").Append(nodesSearched);
            sb.Append(" nps ").Append(nodesSearched * 1000 / timeElapsed);
            sb.Append(" hashfull ").Append(Engine.TranspositionTable.Hashfull());
            sb.Append(" time ").Append(timeElapsed);

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

            Square origin = move.OriginSquare();
            Square destination = move.DestinationSquare();
            MoveType type = move.Type();

            if (type == MoveType.Castling)
                destination = SquareExtensions.MakeSquare(destination > origin ? File.g : File.c, origin.GetRank());

            string moveString = string.Concat(origin, destination);
            if (type == MoveType.Promotion)
                moveString += " pnbrqk"[(int)move.PromotionPiece()];

            return moveString;
        }

        internal struct GoParameters
        {
            public int Perft;

            public List<Move>? Moves;

            public long StartTime;

            public long WhiteTime;
            public long BlackTime;
            public long WhiteIncrement;
            public long BlackIncrement;
            public long MoveTime;
            public int MovesToGo;

            public int Mate;
            public int Depth;
            public long Nodes;

            public bool Infinite;

            public bool RequiresTimeManagement()
            {
                return WhiteTime != 0 || BlackTime != 0;
            }
        }
    }
}
