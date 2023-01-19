using Chessour.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Chessour.Types.Factory;

namespace Chessour
{
    class UCI
    {
        public const int NormalizeToPawn = (int)Value.PawnMG;

        public static void Run(string[] args)
        {
            Position position = new(FEN.StartPosition, new());

            string command;
            do
            {
                IEnumerator<string> enumerator;
                if (args.Length == 0)
                    enumerator = (Console.ReadLine() ?? "quit").Split(' ', options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).AsEnumerable().GetEnumerator();
                else
                    enumerator = args.AsEnumerable().GetEnumerator();

                command = enumerator.MoveNext() ? enumerator.Current : string.Empty;

                switch (command)
                {
                    case "quit":
                    case "stop":
                        Engine.Threads.Stop = true;
                        break;
                    case "uci":
                        const string uciString = $"""
                                                id name {Engine.Name}
                                                id author {Engine.Author}
                                                uciok
                                                """;
                        Console.WriteLine(uciString);
                        break;
                    case "go":
                        Go(enumerator, position);
                        break;
                    case "position":
                        Position(enumerator, position);
                        break;
                    case "isready":
                        Console.WriteLine("readyok");
                        break;
                    case "bench":
                        Bench(enumerator, position);
                        break;
                    case "eval":
                        Eval(position);
                        break;
                    default:
                        Console.WriteLine("Unrecognized command");
                        break;
                }

            } while (command != "quit" && args.Length == 0);
        }

        private static void Position(IEnumerator<string> enumerator, Position position)
        {
            string token = enumerator.MoveNext() ? enumerator.Current : string.Empty;

            string fen;
            if (token == "startpos")
            {
                fen = FEN.StartPosition;
                enumerator.MoveNext();
            }
            else if (token == "fen")
            {
                StringBuilder sb = new(100);

                while (enumerator.MoveNext())
                {
                    if (enumerator.Current == "moves") break;

                    sb.Append(enumerator.Current);
                    sb.Append(' ');
                }

                fen = sb.ToString();
            }
            else
                return;

            position.Set(fen, new());

            //Handle Moves
            while (enumerator.MoveNext())
            {
                Move m = ParseMove(position, enumerator.Current);
                if (m != Move.None)
                    position.MakeMove(m, new Position.StateInfo());
                else
                    return;
            }
        }
        private static void Go(IEnumerator<string> enumerator, Position position)
        {
            SearchContext.SearchLimits limits = new();
            bool ponder = false;

            while (enumerator.MoveNext())
                switch (enumerator.Current)
                {
                    case "searchmoves":
                        while (enumerator.MoveNext())
                            limits.SearchMoves.Add(ParseMove(position, enumerator.Current));
                        break;
                    case "ponder":
                        ponder = true;
                        break;
                    case "depth":
                        limits.Depth = enumerator.MoveNext() ? int.Parse(enumerator.Current) : 0;
                        break;
                    case "perft":
                        limits.Perft = enumerator.MoveNext() ? int.Parse(enumerator.Current) : 0;
                        break;
                }

            Engine.Threads.StartThinking(position, limits, ponder);
        }
        private static void Bench(IEnumerator<string> enumerator, Position position)
        {
            ulong nodes = 0;

            long elapsed = Engine.Now();
            string token = enumerator.MoveNext() ? enumerator.Current : string.Empty;


            if (token == "go")
            {
                Console.WriteLine($"\nPosition ({position.FEN()})");

                Go(enumerator, position);

                Engine.Threads.WaitForSeachFinish();
                nodes += Engine.Threads.NodesSearched();
            }


            elapsed = Engine.Now() - elapsed + 1;

            Console.WriteLine();
            Console.WriteLine($"Total time (ms) : {elapsed}");
            Console.WriteLine($"Nodes searched : {nodes}");
            Console.WriteLine($"Nps : {1000 * nodes / (ulong)elapsed}");
        }
        private static void Eval(Position position)
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
                sb.Append(ToString(pv[i]));
            }

            return sb.ToString();
        }
        public static Move ParseMove(Position position, string str)
        {
            MoveList moveList = new(position, stackalloc MoveScore[MoveList.MaxMoveCount]);

            foreach (Move m in moveList)
                if (str == ToString(m))
                    return m;

            return Move.None;
        }
        public static string ToString(Move m)
        {
            Square from = m.FromSquare();
            Square to = m.ToSquare();

            if (m == Move.None)
                return "(none)";

            if (m == Move.Null)
                return "0000";

            if (m.TypeOf() == MoveType.Castling)
                to = MakeSquare(to > from ? File.g : File.c, from.RankOf());

            string move = string.Concat(from, to);
            if (m.TypeOf() == MoveType.Promotion)
                move += " pnbrqk"[(int)m.PromotionPiece()];

            return move;
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
