using System.Collections.Generic;
using static Chessour.Engine;
using static Chessour.Evaluation;
using static Chessour.Search.DepthConstants;

namespace Chessour.Search
{
    internal partial class SearchThread
    {
        enum NodeType
        {
            Root,
            PV,
            NonPV
        }

        private struct SearchStack
        {
            public bool inCheck;
            public bool ttHit;
            public int evaluation;
            public Move currentMove;
        }


        public readonly List<RootMove> rootMoves;

        public int CompletedDepth { get; protected set; }
        public int SelectiveDepth { get; protected set; }
        public ulong NodeCount { get; protected set; }
        public ulong QNodeCount { get; protected set; }
        public bool Searching { get; protected set; }

        public void ResetSearchStats()
        {
            SelectiveDepth = CompletedDepth = 0;
            NodeCount = 0;
            QNodeCount = 0;
        }

        public void SetPosition(Position position, List<Move> moves)
        {
            rootMoves.Clear();

            this.position.Copy(position);

            if (moves.Count != 0)
            {
                foreach (Move m in moves)
                    rootMoves.Add(new RootMove(m));
            }
            else
            {
                var generatedMoves = MoveGenerator.GenerateLegal(position, stackalloc MoveScore[256]);

                foreach (Move m in generatedMoves)
                    rootMoves.Add(new RootMove(m));
            }
        }

        public ulong Perft(int depth)
        {
            return Perft(depth, 0);

            ulong Perft(int depth, int distanceToRoot)
            {
                var state = states[distanceToRoot];
                bool leaf = depth == 2;

                ulong branchNodes, totalNodes = 0;
                foreach (Move move in MoveGenerator.GenerateLegal(position, stackalloc MoveScore[256]))
                {
                    if (distanceToRoot == 0 && depth == 1)
                        totalNodes += branchNodes = 1;

                    else
                    {
                        position.MakeMove(move, state);
                        branchNodes = leaf ? (ulong)MoveGenerator.GenerateLegal(position, stackalloc MoveScore[256]).Length
                                           : Perft(depth - 1, distanceToRoot + 1);
                        totalNodes += branchNodes;
                        position.Takeback(move);
                    }

                    if (distanceToRoot == 0)
                        Console.WriteLine($"{UCI.Move(move)}: {branchNodes}");
                }

                return totalNodes;
            }
        }

        protected void Search()
        {
            int bestValue = -Infinite;
            int alpha = -Infinite;
            int delta = -Infinite;
            int beta = Infinite;

            Span<SearchStack> stack = stackalloc SearchStack[MAX_PLY];

            for (int depth = 1; depth < DepthConstants.Max; depth++)
            {
                if (Stop)
                    break;

                if (SearchLimits.Depth > 0 && depth > SearchLimits.Depth)
                    break;

                foreach (var rm in rootMoves)
                    rm.PreviousScore = rm.Score;

                SelectiveDepth = 0;

                int failHighCount = 0;
                while (true)
                {
                    bestValue = NodeSearch(NodeType.Root, stack, alpha, beta, 0, depth);

                    rootMoves.Sort(0);

                    if (Stop)
                        break;

                    if ((MasterThread)this is not null)
                    {
                        if (bestValue <= alpha || bestValue >= beta && Engine.TimeManager.Elapsed() > 3000)
                        {
                            UCI.SendPV(this, depth);
                        }
                    }

                    if (bestValue <= alpha) //fail low
                    {
                        beta = (alpha + beta) / 2;
                        alpha = Math.Max(bestValue - delta, -Infinite);

                        failHighCount = 0;
                    }
                    else if (bestValue >= beta) //fail high
                    {
                        beta = Math.Min(bestValue + delta, Infinite);
                        failHighCount++;
                    }
                    else
                        break;

                    delta += delta / 4;

                    Debug.Assert(alpha >= -Infinite && beta <= Infinite);
                }

                if ((MasterThread)this is not null)
                {
                    if (!Stop || Engine.TimeManager.Elapsed() > 3000)
                        UCI.SendPV(this, depth);
                }

                if (!Stop)
                    CompletedDepth = depth;

                if (SearchLimits.Mate > 0
                    && bestValue >= Mate
                    && Mate - bestValue >= 2 * SearchLimits.Mate)
                    Stop = true;

                if (SearchLimits.UseTimeManagement()
                    && !Stop)
                {
                    if (Engine.TimeManager.Elapsed() > Engine.TimeManager.OptimumTime)
                        Stop = true;
                }

                Debug.Assert(-Infinite <= alpha && beta <= Infinite);
            }
        }

        private int NodeSearch(NodeType nodeType, Span<SearchStack> stack, int alpha, int beta, int ply, int depth, Span<Move> pv = default)
        {
            bool IsRoot() => nodeType == NodeType.Root;
            bool IsPV() => nodeType != NodeType.NonPV;

            //If remainingDepth is equal or zero dive into Quisence Search
            if (depth <= 0)
                return QuiescenceSearch(nodeType, stack, alpha, beta, ply, pv);

            if (position.IsDraw())
                return Draw;

            //Check if the alpha and beta values are within acceptable values
            Debug.Assert(-Infinite <= alpha);
            Debug.Assert(alpha < beta);
            Debug.Assert(beta <= Infinite);

            //Check to see if this is a pv node or we are in aspiration window search
            Debug.Assert(IsPV() || (alpha == beta - 1)); 

            //Check to see if depth values are in allowed range
            Debug.Assert(depth > 0 && depth < MAX_DEPTH);

            int score = -Infinite;
            int bestScore = -Infinite;
            Move bestMove = Move.None;
            Span<Move> childPv = stackalloc Move[MAX_PLY]; 

            Position.StateInfo state = states[ply]; 

            stack[ply].inCheck = position.IsCheck();

            CheckTime();

            if (IsPV() && SelectiveDepth < ply + 1)
                SelectiveDepth = ply + 1;

            if (!IsRoot())
            {
                if (Stop)
                    return 0;

                if (ply >= MAX_PLY)
                    return stack[ply].inCheck ? Draw
                                              : Evaluate(position);

                //Mate distance pruning
                alpha = Math.Max(MatedIn(ply), alpha);
                beta = Math.Min(MateIn(ply + 1), beta);
                if (alpha >= beta)
                    return alpha;
            }

            //Transposition table lookup
            Key positionKey = position.PositionKey;
            ref TranspositionTable.Entry ttentry = ref TTTable.ProbeTable(positionKey, out stack[ply].ttHit);
            int ttScore = stack[ply].ttHit ? ttentry.Evaluation : 0;
            Move ttMove = stack[ply].ttHit ? ttentry.Move : Move.None;
            bool ttCapture = ttMove != Move.None && position.IsCapture(ttMove);

            
            if (stack[ply].inCheck)
            {
                stack[ply].evaluation = -Infinite;
                goto movesloop;
            }
            else if (stack[ply].ttHit)
            {
                stack[ply].evaluation = ttentry.Evaluation;
            }
            else
            {
                stack[ply].evaluation = Evaluate(position);
            }

            //Null move reduction
            if (!IsPV()
                && stack[ply - 1].currentMove != Move.Null
                && stack[ply].evaluation >= beta)
            {
                int R = depth / 4 + 3;

                stack[ply].currentMove = Move.Null;

                NodeCount++;
                position.MakeNullMove(state);
                int nullScore = -NodeSearch(NodeType.NonPV, stack, -beta, -alpha, ply + 1, depth - R);
                position.TakebackNullMove();

                if(nullScore >= beta)
                {
                    depth -= 4;
                }        
            }

            movesloop:
            MovePicker movePicker = new(position, ttMove, stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT]);
            int moveCount = 0;
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
                    score = -NodeSearch(NodeType.NonPV, stack, -(alpha + 1), -alpha, ply + 1, newDepth);
                }

                if (IsPV() && (moveCount == 1 || (score > alpha)))
                {
                    childPv[0] = Move.None;

                    score = -NodeSearch(NodeType.PV, stack, -beta, -alpha, ply + 1, newDepth, childPv);
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
                        rootMove.SelectiveDepth = SelectiveDepth;

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
                            if (pvMove == Move.None)
                                break;

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
                        if(score >= beta)
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

            if (moveCount == 0)
            {
                bestScore = stack[ply].inCheck ? MatedIn(ply)
                                             : Draw;
            }

            ttentry.Save(positionKey, IsPV(), bestMove, depth,
                bestScore >= beta ? Bound.Lower
                                       : IsPV() && bestMove != Move.None ? Bound.Exact
                                                                   : Bound.Upper, bestScore);

            return bestScore;
        }

        private int QuiescenceSearch(NodeType nodeType, Span<SearchStack> ss, int alpha, int beta, int ply, Span<Move> pv, int depth = 0)
        {
            bool IsPV() => nodeType != NodeType.NonPV;

            if (position.IsDraw())
                return Draw;


            //Check if the alpha and beta values are within acceptable values
            Debug.Assert(-Infinite <= alpha);
            Debug.Assert(alpha < beta);
            Debug.Assert(beta <= Infinite);

            //This is a pv node or we are in a aspiration search
            Debug.Assert(IsPV() || alpha == beta - 1); 
           
            //Depth is negative
            Debug.Assert(depth <= 0 && depth > TTOffset); 

            Span<Move> childPV = stackalloc Move[MAX_PLY];
            Position.StateInfo state = states[ply];
            int bestValue = -Infinite;
            Move bestMove = Move.None;

            ss[ply].inCheck = position.IsCheck();

            CheckTime();

            if (IsPV())
                pv[0] = Move.None;

            if (ply >= MAX_PLY)
                return ss[ply].inCheck ? Draw : Evaluate(position);

            int ttDepth = ss[ply].inCheck || depth >= QSChecks ? QSChecks
                                                               : QSNoChecks;

            //Transposition table lookup
            Key positionKey = position.PositionKey;
            ref TranspositionTable.Entry ttentry = ref TTTable.ProbeTable(positionKey, out ss[ply].ttHit);
            int ttScore = ss[ply].ttHit ? ttentry.Evaluation : 0;
            Move ttMove = ss[ply].ttHit ? ttentry.Move
                                        : Move.None;
            //Transposition cuttoff
            if (!IsPV()
                && ss[ply].ttHit
                && ttentry.Depth >= ttDepth
                && (ttentry.BoundType & (ttScore >= beta ? Bound.Lower : Bound.Upper)) != 0)
                return ttScore;

            //Static evaluation
            if (ss[ply].inCheck)
            {
                ss[ply].evaluation = 0;
                bestValue = -Infinite;
            }
            else
            {
                //If there is a tt hit use its value
                if (ss[ply].ttHit)
                {
                    if ((ss[ply].evaluation = bestValue = ttentry.Evaluation) == 0)
                        ss[ply].evaluation = bestValue = Evaluate(position);

                    if (ttScore != 0
                        && (ttentry.BoundType & (ttScore > bestValue ? Bound.Lower : Bound.Upper)) != 0)
                        bestValue = ttScore;
                }
                else
                    ss[ply].evaluation = bestValue = Evaluate(position);

                //Stand pat
                if (bestValue >= beta)
                {
                    if (!ss[ply].ttHit)
                        ttentry.Save(positionKey, IsPV(), Move.None, 0, Bound.Lower, ss[ply].evaluation);

                    return bestValue;
                }

                if (IsPV() && bestValue > alpha)
                    alpha = bestValue;
            }
           
            MovePicker movePicker = new(position, ttMove, ss[ply-1].currentMove.DestinationSquare(), stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT]);
            int moveCount = 0;
            foreach (Move move in movePicker)
            {
                if (!position.IsLegal(move))
                    continue;

                moveCount++;
                ss[ply].currentMove = move;
                bool givesCheck = position.GivesCheck(move);

                //If we are not in a desperate situation we can skip the moves that returns a negative Static Exchange Evaluation
                if (bestValue > ExpectedLoss
                    && !position.StaticExchangeEvaluationGE(move))
                    continue;

                NodeCount++;
                position.MakeMove(move, state, givesCheck);

                int score = -QuiescenceSearch(nodeType, ss, -beta, -alpha, ply + 1, childPV, Math.Max(depth - 1, TTOffset + 1));

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

                        if (IsPV() && score < beta)
                            alpha = score;

                        else
                            break;   //Fail high                    
                    }
                }
            }

            //After searching every evasion move if we have found no legal moves and we are in check we are mated
            if (ss[ply].inCheck && moveCount == 0)
            {
                Debug.Assert(MoveGenerator.GenerateLegal(position, stackalloc MoveScore[256]).Length == 0);
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
            var elapsed = Engine.TimeManager.Elapsed();

            if (SearchLimits.UseTimeManagement() && elapsed > Engine.TimeManager.MaxTime)
            {
                Stop = true;
            }
        }

        private static void UpdatePV(Span<Move> pv, Move move, Span<Move> childPv)
        {
            Debug.Assert((ushort)move == (int)move);

            int i = 0, j = 0;
            pv[i++] = move;
            // Has the child pv ended
            while (childPv[j] != Move.None) 
            {              
                pv[i++] = childPv[j++]; // Copy the moves from the child pv
            }

            pv[i] = Move.None; // Put this at the end to represent pv line ended
        }
    }
}