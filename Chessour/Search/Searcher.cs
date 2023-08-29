using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using static Chessour.Engine;
using static Chessour.Evaluation.Evaluator;
using static Chessour.Search.DepthConstants;

namespace Chessour.Search
{
    public enum NodeType { Root, PV, NonPV}

    internal class Searcher
    {
        public Searcher()
        {
            rootState = new();
            position = new(rootState);

            states = new Position.StateInfo[MAX_PLY];
            for (int i = 0; i < MAX_PLY; i++)
                states[i] = new();

            rootMoves = new();
        }

        public List<RootMove> rootMoves;

        private readonly Position position;
        private readonly Position.StateInfo rootState;
        private readonly Position.StateInfo[] states;

        private int rootDepth;
        private int completedDepth;
        private int selectiveDepth;

        public bool SendInfo { get; set; }
        public ulong NodeCount { get; set; }

        public void SetSearchParameters(Position position, List<Move>? moves)
        {
            this.rootMoves.Clear();
            this.position.Copy(position);

            if (moves is not null)
                foreach (Move move in moves)
                    rootMoves.Add(new RootMove(move));
            else
                foreach (Move move in MoveGenerators.Legal.Generate(position, stackalloc MoveScore[256]))
                    rootMoves.Add(new RootMove(move));
        }

        public void ResetSearchStats()
        {
            completedDepth = selectiveDepth = 0;
            NodeCount = 0;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2014:Do not use stackalloc in loops", Justification = "Its fine")]
        public ulong Perft(int depth)
        {
            ulong totalNodes = 0, branchNodes;
            var state = states[0];

            if(depth > 2)
            {
                foreach (Move move in MoveGenerators.Legal.Generate(position, stackalloc MoveScore[256]))
                {
                    position.MakeMove(move, state);
                    totalNodes += branchNodes = Perft(depth - 1, 1);
                    position.Takeback(move);

                    Console.WriteLine($"{UCI.Move(move)}: {branchNodes}");
                }
            }
            else if(depth == 2)
            {
                foreach (Move move in MoveGenerators.Legal.Generate(position, stackalloc MoveScore[256]))
                {
                    position.MakeMove(move, state);
                    totalNodes += branchNodes = (ulong)MoveGenerators.Legal.Generate(position, stackalloc MoveScore[256]).Length;
                    position.Takeback(move);

                    Console.WriteLine($"{UCI.Move(move)}: {branchNodes}");
                }
            }
            else
            {
                foreach (Move move in MoveGenerators.Legal.Generate(position, stackalloc MoveScore[256]))
                {
                    totalNodes++;

                    Console.WriteLine($"{UCI.Move(move)}: 1");
                }
            }

            return NodeCount = totalNodes;
        }

        protected ulong Perft(int depth, int distance)
        {
            ulong totalNodes = 0;
            var state = states[distance];         

            if(depth != 2)
            {
                foreach (Move move in MoveGenerators.Legal.Generate(position, stackalloc MoveScore[256]))
                {
                    position.MakeMove(move, state);
                    totalNodes += Perft(depth - 1, distance + 1);
                    position.Takeback(move);
                }
            }
            else
            {
                foreach (Move move in MoveGenerators.Legal.Generate(position, stackalloc MoveScore[256]))
                {
                    position.MakeMove(move, state);
                    totalNodes += (ulong)MoveGenerators.Legal.Generate(position, stackalloc MoveScore[256]).Length;
                    position.Takeback(move);
                }
            }

            return totalNodes;
        }

        public void Search()
        {
            ResetSearchStats();

            Span<SearchStack> stack = stackalloc SearchStack[MAX_PLY];

            int bestValue = -Infinite;
            int alpha = -Infinite;
            int beta = Infinite;

            int maxPvIdx = Math.Min(1, rootMoves.Count);

            for (rootDepth = 1; rootDepth < DepthConstants.Max; rootDepth++)
            {
                if (Stop)
                    break;

                if (SearchLimits.Mate > 0 && rootDepth > SearchLimits.Mate)
                    break;

                foreach (var rootMove in rootMoves)
                    rootMove.PreviousScore = rootMove.Score;

                for (int pvIdx = 0; pvIdx < maxPvIdx; pvIdx++)
                {
                    selectiveDepth = 0;

                    bestValue = NodeSearch(NodeType.Root, stack, 0, alpha, beta, rootDepth);

                    rootMoves.Sort(pvIdx);

                    if (Stop)
                        break;

                    if (SendInfo && !Stop)
                    {
                        UCI.SendPV(this, rootDepth);
                    }
                }

                if (!Stop)
                    completedDepth = rootDepth;

                if (SearchLimits.Mate > 0
                    && bestValue >= MateValue
                    && MateValue - bestValue >= 2 * SearchLimits.Mate)
                    Stop = true;

                if (SearchLimits.RequiresTimeManagement() && Timer.Elapsed() > Timer.OptimumTime)
                    Stop = true;
            }
        }

        private int NodeSearch(NodeType nodeType, Span<SearchStack> stack, int ply, int alpha, int beta, int depth, Span<Move> pv = default)
        {
            //If remainingDepth is equal or zero dive into Quisence Search
            if (depth <= 0)
                return QuiescenceSearch(nodeType, stack, ply, alpha, beta, 0, pv);

            if (position.IsDraw())
                return DrawValue;

            //Check if the alpha and beta values are within acceptable values
            Debug.Assert(-Infinite <= alpha);
            Debug.Assert(alpha < beta);
            Debug.Assert(beta <= Infinite);

            //Check to see if this is a pv node or we are in aspiration window search
            Debug.Assert(IsPV() || (alpha == beta - 1));

            //Check to see if depth values are in allowed range
            Debug.Assert(depth > 0 && depth < MAX_DEPTH);

            bool IsRoot() => nodeType == NodeType.Root;
            bool IsPV() => nodeType != NodeType.NonPV;

            int score = -Infinite;
            int bestScore = -Infinite;
            Move bestMove = Move.None;
            Span<Move> childPv = stackalloc Move[MAX_PLY];

            Position.StateInfo state = states[ply];

            bool inCheck = stack[ply].inCheck = position.IsCheck();

            CheckTime();

            if (IsPV())
                selectiveDepth = Math.Max(selectiveDepth, ply + 1);

            if (!IsRoot())
            {
                if (Stop)
                    return 0;

                if (ply >= MAX_PLY)
                    return inCheck ? DrawValue
                                   : Evaluate(position);

                //Mate distance pruning
                alpha = Math.Max(MatedIn(ply), alpha);
                beta = Math.Min(MateIn(ply + 1), beta);
                if (alpha >= beta)
                    return alpha;
            }

            //Transposition table lookup
            Key positionKey = position.PositionKey;
            ref TranspositionTable.Entry ttentry = ref TTTable.ProbeTable(positionKey, out bool ttHit);
            int ttScore = 0;
            Move ttMove = Move.None;
            bool ttCapture = false;

            stack[ply].ttHit = ttHit;
            if (ttHit)
            {
                ttScore = ttentry.Evaluation;
                ttMove = ttentry.Move;
                ttCapture = ttMove != Move.None && position.IsCapture(ttMove);
            }

            int evaluation = 0;

            if (inCheck)
            {
                evaluation = stack[ply].evaluation = -Infinite;
            }
            else
            {
                if (ttHit)
                {
                    evaluation = stack[ply].evaluation = ttentry.Evaluation;
                }
                else
                {
                    evaluation = stack[ply].evaluation = Evaluate(position);
                }

                if (!IsPV())
                {
                    //Futility Pruning
                    if (depth < 5
                        && evaluation >= beta
                        && evaluation < ExpectedWin + 1)
                        return evaluation;

                    //Null move reduction
                    if (stack[ply - 1].currentMove != Move.Null
                        && evaluation >= beta)
                    {
                        int R = (depth / 4) + 3;

                        stack[ply].currentMove = Move.Null;

                        NodeCount++;
                        position.MakeNullMove(state);
                        int nullScore = -NodeSearch(NodeType.NonPV, stack, ply + 1, -beta, -alpha, depth - R);
                        position.TakebackNullMove();

                        if (nullScore >= beta)
                        {
                            depth -= 4;
                        }
                    }
                }

            }

            MovePicker movePicker = new(position, ttMove, stackalloc MoveScore[MoveGenerators.MAX_MOVE_COUNT]);
            int moveCount = 0;
            int nextPly = ply + 1;
            foreach (Move move in movePicker)
            {
                if (IsRoot())
                {
                    if (!rootMoves.Contains(move))
                        continue;
                }
                else if (!position.IsLegal(move))
                    continue;

                moveCount++;
                stack[ply].currentMove = move;
                bool givesCheck = position.GivesCheck(move);

                NodeCount++;
                position.MakeMove(move, state, givesCheck);

                int newDepth = depth - 1;

                if (!IsPV() || moveCount > 1)
                {
                    score = -NodeSearch(NodeType.NonPV, stack, nextPly, -(alpha + 1), -alpha, newDepth);
                }

                if (IsPV() && (moveCount == 1 || (score > alpha)))
                {
                    childPv[0] = Move.None;
                    score = -NodeSearch(NodeType.PV, stack, nextPly, -beta, -alpha, newDepth, childPv);
                }

                position.Takeback(move);

                if (Stop)
                    return 0;

                Debug.Assert(score > -Infinite && score < Infinite);
                if (IsRoot())
                {
                    var rootMove = rootMoves.Find(move)!;
                    if (moveCount == 1 || score > alpha)
                    {
                        rootMove.Score = rootMove.UCIScore = score;
                        rootMove.SelectiveDepth = selectiveDepth;

                        rootMove.BoundLower = rootMove.BoundUpper = false;

                        if (score >= beta)
                        {
                            rootMove.BoundLower = true;
                            rootMove.UCIScore = beta;
                        }
                        else if (score <= alpha)
                        {
                            rootMove.BoundUpper = true;
                            rootMove.UCIScore = alpha;
                        }
                        
                        rootMove.PV.Clear();
                        rootMove.PV.Add(move);
                        
                        foreach (Move pvMove in childPv)
                        {
                            if (pvMove == Move.None) break;

                            rootMove.PV.Add(pvMove);
                        }
                    }
                    else
                        rootMove.Score = -Infinite;
                }

                if (score > bestScore)
                {
                    bestScore = score;

                    if (score > alpha)
                    {
                        bestMove = move;

                        if (IsPV() && !IsRoot())
                        {
                            UpdatePV(pv, move, childPv);
                        }
                        if (score >= beta)
                        {
                            break;
                        }
                        else
                        {
                            alpha = score;

                            if (depth > 1
                                && depth < 6
                                && beta < ExpectedWin
                                && alpha > -ExpectedWin)
                                depth -= 1;

                            Debug.Assert(depth > 0);
                        }
                    }
                }
            }

            //If there is no legal moves in the position we are either mated or its stealmate
            if (moveCount == 0)
            {
                bestScore = inCheck ? MatedIn(ply)
                                    : DrawValue;
            }

            ttentry.Save(positionKey, IsPV(), bestMove, depth,
                bestScore >= beta ? Bound.Lower
                                  : IsPV() && bestMove != Move.None ? Bound.Exact
                                                                    : Bound.Upper, bestScore);

            return bestScore;
        }

        private int QuiescenceSearch(NodeType nodeType, Span<SearchStack> stack, int ply, int alpha, int beta, int depth = 0, Span<Move> pv = default)
        {
            bool IsPV() => nodeType != NodeType.NonPV;

            if (position.IsDraw())
                return DrawValue;

            //Check if the alpha and beta values are within acceptable values
            Debug.Assert(-Infinite <= alpha);
            Debug.Assert(alpha < beta);
            Debug.Assert(beta <= Infinite);

            //This is a pv node or we are in a aspiration search
            Debug.Assert(IsPV() || alpha == beta - 1);

            //Depth is negative
            Debug.Assert(depth <= 0 && depth > TTOffset);

            //Check to see if this position repeated during search
            //If yes we can claim this position as Draw
            if (position.FiftyMoveCounter >= 3
                && alpha < DrawValue
                && position.HasRepeated(ply))
            {
                alpha = DrawValue;
                if (alpha >= beta)
                    return alpha;
            }

            Span<Move> childPV = stackalloc Move[MAX_PLY];
            Position.StateInfo state = states[ply];
            int bestValue = -Infinite;
            Move bestMove = Move.None;
            bool inCheck = stack[ply].inCheck = position.IsCheck();

            CheckTime();

            if (IsPV())
                pv[0] = Move.None;

            if (ply >= MAX_PLY)
                return inCheck ? DrawValue : Evaluate(position);

            int ttDepth = (inCheck || depth >= QSChecks) ? QSChecks : QSNoChecks;


            //Transposition table lookup
            Key positionKey = position.PositionKey;
            ref TranspositionTable.Entry ttentry = ref TTTable.ProbeTable(positionKey, out bool ttHit);
            int ttScore = 0;
            Move ttMove = Move.None;
            bool ttCapture = false;

            stack[ply].ttHit = ttHit;
            if (ttHit)
            {
                ttScore = ttentry.Evaluation;
                ttMove = ttentry.Move;
                ttCapture = ttMove != Move.None && position.IsCapture(ttMove);
            }

            //Transposition cuttoff
            if (!IsPV()
                && ttHit
                && ttentry.Depth >= ttDepth
                && (ttentry.BoundType & (ttScore >= beta ? Bound.Lower : Bound.Upper)) != 0)
                return ttScore;

            //Static evaluation
            if (inCheck)
            {
                stack[ply].evaluation = 0;
                bestValue = -Infinite;
            }
            else
            {
                //If there is a tt hit use its value
                if (ttHit)
                {
                    if ((stack[ply].evaluation = bestValue = ttScore) == 0)
                        stack[ply].evaluation = bestValue = Evaluate(position);

                    if (ttScore != 0
                        && (ttentry.BoundType & (ttScore > bestValue ? Bound.Lower : Bound.Upper)) != 0)
                        bestValue = ttScore;
                }
                else
                    stack[ply].evaluation = bestValue = Evaluate(position);

                //Stand pat
                if (bestValue >= beta)
                {
                    if (!ttHit)
                        ttentry.Save(positionKey, IsPV(), Move.None, 0, Bound.Lower, stack[ply].evaluation);

                    return bestValue;
                }

                if (IsPV() && bestValue > alpha)
                    alpha = bestValue;
            }

            MovePicker movePicker = new(position, ttMove, stack[ply - 1].currentMove.DestinationSquare(), stackalloc MoveScore[MoveGenerators.MAX_MOVE_COUNT]);
            int moveCount = 0;
            foreach (Move move in movePicker)
            {
                if (!position.IsLegal(move))
                    continue;

                moveCount++;
                stack[ply].currentMove = move;
                bool givesCheck = position.GivesCheck(move);

                //If we are not in a desperate situation we can skip the moves that returns a negative Static Exchange Evaluation
                if (bestValue > ExpectedLoss
                    && !position.StaticExchangeEvaluationGE(move))
                    continue;

                NodeCount++;
                position.MakeMove(move, state, givesCheck);

                if (IsPV())
                    childPV[0] = Move.None;

                int score = -QuiescenceSearch(nodeType, stack, ply + 1, -beta, -alpha, Math.Max(depth - 1, TTOffset + 1), childPV);

                position.Takeback(move);

                if (Stop)
                    return 0;

                Debug.Assert(-Infinite < score && score < Infinite);

                if (score > bestValue)
                {
                    bestValue = score;

                    if (score > alpha)
                    {
                        bestMove = move;

                        if (IsPV())
                            UpdatePV(pv, move, childPV);

                        if (score < beta)
                            alpha = score;
                        else
                            break;   //Fail high                    
                    }
                }
            }

            //After searching every evasion move if we have found no legal moves and we are in check we are mated
            if (inCheck && moveCount == 0)
            {
                Debug.Assert(MoveGenerators.Legal.Generate(position, stackalloc MoveScore[256]).Length == 0);
                return MatedIn(ply);
            }

            ttentry.Save(positionKey, IsPV(), bestMove, ttDepth,
                bestValue >= beta ? Bound.Lower
                                 : IsPV() && bestMove != Move.None ? Bound.Exact
                                                                   : Bound.Upper, bestValue);

            return bestValue;
        }

        private static void CheckTime()
        {
            if(SearchLimits.RequiresTimeManagement() && Timer.Elapsed() > Timer.MaxTime)
                Stop = true;
        }

        private static void UpdatePV(Span<Move> pv, Move move, Span<Move> childPv)
        {
            int i = 0, j = 0;
            pv[i++] = move;

            for (; childPv[j] != Move.None; i++, j++)
                pv[i] = childPv[j];

            pv[i] = Move.None;
        }

        private struct SearchStack
        {
            public bool inCheck;
            public bool ttHit;
            public int evaluation;
            public Move currentMove;
        }
    }
}