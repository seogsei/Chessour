using Chessour.Evaluation;
using Chessour.MoveGeneration;
using Chessour.Search;
using System.Text;

namespace Chessour;

internal static class UCI
{
    public const string StartFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";     
    public const int NormalizeToPawn = Chessour.Evaluation.ValueConstants.PawnMGValue;

    public static string PV(SearchThread bestThread, Depth depth)
    {
        StringBuilder sb = new(1024);

        RootMoves rootMoves = bestThread.rootMoves;
        ulong nodesSearched = Engine.Threads.NodesSearched;
        var elapsed = Engine.Time.Elapsed + 1;

        sb.Append("info");
        sb.Append(" depth ");
        sb.Append(depth);
        sb.Append(" seldepth ");
        sb.Append(rootMoves[0].SelectiveDepth);
        sb.Append(" score ");
        sb.Append(rootMoves[0].UCIScore.ToUCIString());

        sb.Append(" nodes ");
        sb.Append(nodesSearched);
        sb.Append(" nps ");
        sb.Append(nodesSearched * 1000 / (ulong)elapsed);
        sb.Append(" time ");
        sb.Append(elapsed);

        sb.Append(" pv");
        foreach (Move m in rootMoves[0].pv)
        {
            sb.Append(' ');
            sb.Append(m);
        }     

        return sb.ToString();
    }

    public static Move ParseMove(Position position, string str)
    {
        MoveList moveList = new(position, stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT]);

        foreach (Move m in moveList)
            if (str == m.ToString())
                return m;

        return Move.None;
    }

    public static void Loop(string[] args)
    {
        Position position = new(StartFEN, new());

        string command;
        do
        {
            UCIStream ss = args.Length == 0 ? new(Console.ReadLine() ?? "quit")
                                               : new(string.Join(' ', args));

            ss.SkipWhiteSpace();
            ss.Extract(out command);

            switch (command)
            {
                case "uci":
                    Console.WriteLine($"id name {Engine.Name}");
                    Console.WriteLine($"id author {Engine.Author}");
                    Console.WriteLine($"uciok");
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
                    Engine.Stop = true;
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

    private static void Position(ref UCIStream ss, Position position)
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

    private static void Go(ref UCIStream ss, Position position)
    {
        Search.Limits limits = new()
        {
            startTime = Engine.Now
        };

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

        Engine.StartThinking(position, in limits);
    }

    private static void Eval(Position position)
    {
        Evaluator.Evaluate(position, true);

        Console.WriteLine(Evaluator.Trace.ToString());
    }

    private static void Bench(ref UCIStream ss, Position position)
    {
        ulong nodes = 0;

        long elapsed = Engine.Now;
        ss.Extract(out string token);

        if (token == "go")
        {
            Console.WriteLine($"\nPosition ({position.FEN()})");

            Go(ref ss, position);

            Engine.Threads.WaitForSeachFinish();
            nodes += Engine.Threads.NodesSearched;
        }

        elapsed = Engine.Now - elapsed + 1;

        Console.WriteLine($"""

                Total time (ms) : {elapsed}
                Nodes searched : {nodes}
                Nps : {1000 * nodes / (ulong)elapsed}
                """);
    }
}
