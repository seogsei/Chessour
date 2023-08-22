using Chessour.Evaluation;
using System.Collections;
using System.Collections.Generic;
using static Chessour.Engine;
using static Chessour.Evaluation.Evaluator;
using static Chessour.Search.DepthConstants;
using static System.Formats.Asn1.AsnWriter;

namespace Chessour.Search
{
    internal partial class SearchThread
    {
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
                    bestValue = RootSearch(stack, alpha, beta, depth);

                    //bestValue = NodeSearch(NodeType.Root, stacks, alpha, beta, 0, depth);

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

                Debug.Assert(alpha >= -Infinite && beta <= Infinite);
            }
        }

        private struct SearchStack
        {
            public bool inCheck;
            public bool ttHit;
            public int evaluation;
            public Move currentMove;
        }

        private int RootSearch(Span<SearchStack> stack, int alpha, int beta, int depth, int startIndex = 0)
        {
            Span<Move> childPv = stackalloc Move[MAX_PLY];
            var state = states[0];

            int score = -Infinite;
            int bestScore = -Infinite;

            for (int i = startIndex; i < rootMoves.Count; i++)
            {
                RootMove rootMove = rootMoves[i];
                Move move = rootMove.Move;

                bool givesCheck = position.GivesCheck(move);

                position.MakeMove(move, state, givesCheck);

                if (i > startIndex)
                {
                    score = -NodeSearch(false, stack, -(alpha + 1), -alpha, 1, depth - 1);
                }
                if (i == startIndex || (score > alpha))
                {
                    //Reset the pv
                    childPv[0] = Move.None;
                    score = -NodeSearch(true, stack, -beta, -alpha, 1, depth - 1, childPv); 
                }

                position.Takeback(move);

                if (Stop)
                    return 0;

                Debug.Assert(score > -Infinite && score < Infinite);

                if (i == startIndex || score > alpha)
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
                        Debug.Assert(pvMove != Move.Null);

                        if (pvMove == Move.None)
                            break;

                        rootMove.PV.Add(pvMove);
                    }
                }
                else
                    rootMove.Score = -Infinite;

                if (score > bestScore)
                {
                    bestScore = score;

                    if (score > alpha)
                    {
                        if (score < beta)
                        {
                            alpha = score;

                            if (depth > 1
                            && depth < 6
                            && beta < ExpectedWin
                            && alpha > -ExpectedWin)
                                depth -= 1;

                            Debug.Assert(depth > 0);
                        }
                        else
                        {
                            Debug.Assert(score >= beta);
                            break;
                        }
                    }
                }
            }

            return bestScore;
        }

        private int NodeSearch(bool pvNode, Span<SearchStack> stack, int alpha, int beta, int ply, int depth, Span<Move> pv = default)
        {
            //If remainingDepth is equal or zero dive into Quisence Search
            if (depth <= 0)
                return QuiescenceSearch(pvNode, stack, alpha, beta, ply, pv);

            NodeCount++;

            if (position.IsDraw())
                return Draw;

            //Check if the alpha and beta values are within acceptable values
            Debug.Assert(-Infinite <= alpha);
            Debug.Assert(alpha < beta);
            Debug.Assert(beta <= Infinite);

            //Check to see if this is a pv node or we are in aspiration window search
            Debug.Assert(pvNode || (alpha == beta - 1)); 

            //Check to see if depth values are in allowed range
            Debug.Assert(depth > 0 && depth < MAX_DEPTH);


            int score = -Infinite;
            int bestScore = -Infinite;
            Move bestMove = Move.None;
            Span<Move> childPv = stackalloc Move[MAX_PLY]; 

            Position.StateInfo state = states[ply]; 

            stack[ply].inCheck = position.IsCheck();

            CheckTime();

            if (pvNode && SelectiveDepth < ply + 1)
                SelectiveDepth = ply + 1;

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

            
            //Futility pruning
            if (depth < 5
                && !pvNode
                && stack[ply].evaluation >= beta
                && stack[ply].evaluation < ExpectedWin + 1)
                return stack[ply].evaluation;
            

            //Null move search
            if (!pvNode
                && stack[ply - 1].currentMove != Move.Null
                && depth > 2
                && stack[ply].evaluation >= beta)
            {
                int reduction = 2;
                if (depth > 6)
                    reduction++;

                stack[ply].currentMove = Move.Null;

                position.MakeNullMove(state);

                int nullScore = -NodeSearch(false, stack, -(alpha + 1), -alpha, ply + 1, depth - reduction - 1);

                position.TakebackNullMove();

                if (nullScore >= beta)
                    return nullScore;
            }

            movesloop:
            MovePicker movePicker = new(position, ttMove, stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT]);
            int moveCount = 0;
            foreach (Move move in movePicker) 
            {
                if (!position.IsLegal(move))
                    continue;

                moveCount++;
                stack[ply].currentMove = move;
                bool givesCheck = position.GivesCheck(move);

                if (pvNode)
                    childPv[0] = Move.None;

                position.MakeMove(move, states[ply], givesCheck);

                if (!pvNode || moveCount > 1)
                {
                    score = -NodeSearch(false, stack, -(alpha + 1), -alpha, ply + 1, depth - 1);
                }

                if (pvNode
                    && (moveCount == 1
                    || (score > alpha
                        && (score < beta))))
                {
                    childPv[0] = Move.None;

                    score = -NodeSearch(true, stack, -beta, -alpha, ply + 1, depth - 1, childPv);
                }

                position.Takeback(move);

                if (Stop)
                    return 0;

                Debug.Assert(score > -Infinite && score < Infinite);

                if (score > bestScore)
                {
                    bestScore = score;

                    if (score > alpha)
                    {
                        bestMove = move;

                        if (pvNode)
                        {
                            UpdatePV(pv, move, childPv);
                        }

                        if (pvNode && score < beta)
                        {
                            alpha = score;

                            if (depth > 1
                            && depth < 6
                            && beta < ExpectedWin
                            && alpha > -ExpectedWin)
                                depth -= 1;

                            Debug.Assert(depth > 0);
                        }
                        else
                        {
                            Debug.Assert(score >= beta);
                            break;
                        }
                    }
                }
            }

            if (moveCount == 0)
            {
                bestScore = stack[ply].inCheck ? MatedIn(ply)
                                             : Draw;
            }

            ttentry.Save(positionKey, pvNode, bestMove, depth,
                bestScore >= beta ? Bound.Lower
                                       : pvNode && bestMove != Move.None ? Bound.Exact
                                                                   : Bound.Upper, bestScore);

            return bestScore;
        }

        private int QuiescenceSearch(bool pvNode, Span<SearchStack> ss, int alpha, int beta, int ply, Span<Move> pv, int depth = 0)
        {
            NodeCount++;
            QNodeCount++;

            if (position.IsDraw())
                return Draw;


            //Check if the alpha and beta values are within acceptable values
            Debug.Assert(-Infinite <= alpha);
            Debug.Assert(alpha < beta);
            Debug.Assert(beta <= Infinite);

            //This is a pv node or we are in a aspiration search
            Debug.Assert(pvNode || alpha == beta - 1); 
           
            //Depth is negative
            Debug.Assert(depth <= 0 && depth > TTOffset); 

            Span<Move> childPV = stackalloc Move[MAX_PLY];
            Position.StateInfo state = states[ply];
            int bestValue = -Infinite;
            Move bestMove = Move.None;

            ss[ply].inCheck = position.IsCheck();

            CheckTime();

            if (pvNode)
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
            if (!pvNode
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
                        ttentry.Save(positionKey, pvNode, Move.None, 0, Bound.Lower, ss[ply].evaluation);

                    return bestValue;
                }

                if (pvNode && bestValue > alpha)
                    alpha = bestValue;
            }
           
            MovePicker movePicker = new(position, ttMove, Square.a1, stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT]);
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

                position.MakeMove(move, state, givesCheck);

                int score = -QuiescenceSearch(pvNode, ss, -beta, -alpha, ply + 1, childPV, Math.Max(depth - 1, TTOffset + 1));

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

                        if (pvNode)
                            UpdatePV(pv, move, childPV);

                        if (pvNode && score < beta)
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

            ttentry.Save(positionKey, pvNode, bestMove, ttDepth,
                bestValue >= beta ? Bound.Lower
                                 : pvNode && bestMove != Move.None ? Bound.Exact
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
            int i = 0, j = 0;
            pv[i++] = move;
            while (childPv[j] != Move.None) // Has the child pv ended
                pv[i++] = childPv[j++]; // Copy the moves from the child pv
            pv[i] = Move.None; // Put this at the end to represent pv line ended
        }
    }
}