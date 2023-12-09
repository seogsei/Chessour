using Chessour.Evaluation;
using System.Collections.Generic;
using static Chessour.Engine;
using static Chessour.Evaluation.Evaluator;
using static Chessour.Search.DepthConstants;

namespace Chessour.Search
{
    internal class Searcher
    {
        static Searcher()
        {
            for (int i = 0; i < reductions.Length; i++)
                reductions[i] = MathF.Log(i);
        }

        private static readonly float[] reductions = new float[MAX_PLY];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Extensions(int depth, bool givesCheck)
        {
            if (givesCheck && depth > 7)
                return 1;

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Reductions(int moveCount, int depth)
        {
            int R = (int)(reductions[moveCount] * reductions[depth]);

            return R;
        }
        

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

        private int completedDepth;
        private int selectiveDepth;

        //We can use the last 12 bits of a MOVE to index this 
        private readonly ButterflyTable butterflyTable = new(); 

        public bool SendInfo { get; set; }
        public long NodeCount { get; private set; }
        public int RootDepth { get; private set; }

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

        public void ClearHistoryTables()
        {
            butterflyTable.Clear();
        }

        public long Perft(int depth)
        {
            return NodeCount = Perft(depth, 0);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2014:Do not use stackalloc in loops", Justification = "")]
        protected long Perft(int depth, int distance)
        {
            long totalNodes = 0, branchNodes;
            var state = states[distance];

            foreach (Move move in MoveGenerators.Legal.Generate(position, stackalloc MoveScore[MoveGenerators.MaxMoveCount]))
            {
                if (depth == 1)
                    totalNodes += branchNodes = 1;

                else
                {
                    position.MakeMove(move, state);
                    branchNodes = depth == 2 ? MoveGenerators.Legal.Generate(position, stackalloc MoveScore[MoveGenerators.MaxMoveCount]).Length
                                             : Perft(depth - 1, distance + 1);
                    totalNodes += branchNodes;
                    position.Takeback(move);
                }

                if (distance == 0)
                    Console.WriteLine($"{UCI.Move(move)}: {branchNodes}");
            }

            return totalNodes;
        }

        public void Search()
        {
            RootDepth = 0;
            NodeCount = 0;
            completedDepth = selectiveDepth = 0;

            Span<SearchStack> stack = stackalloc SearchStack[MAX_PLY];
            
            SortRootMoves();

            int bestValue = -InfiniteScore;

            int maxPvIdx = Math.Min(1, rootMoves.Count);
            while (++RootDepth < MaxDepth
                && !Stop
                && !(SearchLimits.depth > 0 && RootDepth > SearchLimits.depth))
            {
                foreach (var rootMove in rootMoves)
                    rootMove.PreviousScore = rootMove.Score;

                for (int pvIdx = 0; pvIdx < maxPvIdx && !Stop; pvIdx++)
                {
                    selectiveDepth = 0;

                    int previousScore = rootMoves[pvIdx].AvarageScore;

                    int delta = Pieces.PawnValue / 2 + (previousScore * previousScore) / 16384;
                    int alpha = Math.Max(previousScore - delta, -InfiniteScore);
                    int beta = Math.Min(previousScore + delta, +InfiniteScore);

                    while (true)
                    {
                        bestValue = RootSearch(stack, alpha, beta, RootDepth);

                        rootMoves.Sort(pvIdx);
                        if (Stop)
                            break;

                        if (bestValue <= alpha) //Fail low
                        {
                            alpha = Math.Max(bestValue - delta, -InfiniteScore);
                        }
                        else if (bestValue >= beta) //Fail high
                        {
                            beta = Math.Min(bestValue + delta, +InfiniteScore);
                        }
                        else
                            break;

                        delta = beta - alpha;

                        Debug.Assert(-InfiniteScore <= alpha && beta <= InfiniteScore);
                    }

                    if (SendInfo
                        && (Stop || pvIdx + 1 == maxPvIdx))
                    {
                        UCI.SendPV(this, RootDepth);
                    }
                }

                if (!Stop)
                    completedDepth = RootDepth;

                if (SearchLimits.mate > 0
                    && bestValue >= MateScore
                    && MateScore - bestValue >= 2 * SearchLimits.mate)
                    Stop = true;

                if (SearchLimits.RequiresTimeManagement() && Timer.Elapsed() > Timer.OptimumTime)
                    Stop = true;
            }
        }

        private void SortRootMoves()
        {

            //Sort root moves before the first iteration of search
            int sortingScore = 0;
            MovePicker movePicker = new(position, Move.None, butterflyTable, stackalloc MoveScore[256]);
            foreach (var move in movePicker)
            {
                RootMove? rootMove = rootMoves.Find(move);

                if (rootMove is null)
                    continue;

                rootMove.Score = sortingScore--;
            }

            rootMoves.Sort(0);
        }

        private int RootSearch(Span<SearchStack> stack, int alpha, int beta, int depth)
        {
            //Check if the alpha and beta values are within acceptable values
            Debug.Assert(-InfiniteScore <= alpha);
            Debug.Assert(alpha < beta);
            Debug.Assert(beta <= InfiniteScore);

            //Check to see if depth values are in allowed range
            Debug.Assert(depth > 0 && depth < MAX_DEPTH);

            int score = -InfiniteScore;
            int evaluation = -InfiniteScore;
            Span<Move> childPv = stackalloc Move[MAX_PLY];
            Position.StateInfo state = states[0];
            bool inCheck = stack[0].inCheck = position.IsCheck();

            selectiveDepth = 1;

            if (inCheck)
                evaluation = stack[0].evaluation = -InfiniteScore;
            else
                evaluation = stack[0].evaluation = Evaluate(position);

            int moveCount = 0;
            foreach (var rootMove in rootMoves)
            {
                Move move = rootMove.Move;

                moveCount++;
                stack[0].currentMove = move;
                Piece piece = position.PieceAt(move.Origin());
                bool givesCheck = position.GivesCheck(move);
                bool isCapture = position.IsCapture(move);

                if(Timer.Elapsed() > TimeSpan.FromSeconds(3))
                    Console.WriteLine($"info depth {depth} currmove {UCI.Move(move)} currmovenumber {moveCount}");

                int newDepth = depth - 1;

                newDepth += Extensions(depth, givesCheck);

                NodeCount++;
                position.MakeMove(move, state, givesCheck);

                int reduction = Reductions(moveCount, depth);

                //LMR
                if (depth > 2
                    && moveCount > 1
                    && (!isCapture))
                {
                    score = -ZWSearch(stack, 1, -(alpha + 1), newDepth - reduction);

                    if (score > alpha)
                    {
                        score = -ZWSearch(stack, 1, -(alpha + 1), newDepth);
                    }
                }
                else if (moveCount > 1)
                {
                    score = -ZWSearch(stack, 1, -(alpha + 1), newDepth);
                }

                if (moveCount == 1 || (score > alpha))
                {
                    childPv[0] = Move.None;
                    score = -PVSearch(stack, 1, -beta, -alpha, newDepth, childPv);
                }

                position.Takeback(move);

                if (Stop)
                    return 0;

                if (moveCount == 1 || score > alpha)
                {
                    rootMove.Score = rootMove.UCIScore = score;
                    rootMove.SelectiveDepth = selectiveDepth;

                    rootMove.AvarageScore = rootMove.AvarageScore != -InfiniteScore ? (rootMove.AvarageScore + score) / 2
                                                                                    : score;
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
                    rootMove.Score = -InfiniteScore;

                if (score > alpha)
                {
                    alpha = score;

                    if (alpha >= beta) //Fail high which means we need to research with a bigger aspiration window
                        break;

                    if (depth > 1
                        && depth < 6
                        && beta < ExpectedWin
                        && score > ExpectedLoss)
                        depth -= 1;

                    Debug.Assert(depth > 0);
                }
            }

            return alpha;
        }

        private int PVSearch(Span<SearchStack> stack, int ply, int alpha, int beta, int depth, Span<Move> pv)
        {
            //If remainingDepth is equal or zero dive into Quisence Search
            if (depth <= 0)
                return QuiescenceSearch(true, stack, ply, alpha, beta, 0, pv);

            //Check if the alpha and beta values are within acceptable values
            Debug.Assert(-InfiniteScore <= alpha);
            Debug.Assert(alpha < beta);
            Debug.Assert(beta <= InfiniteScore);

            //Check to see if depth values are in allowed range
            Debug.Assert(depth > 0 && depth < MAX_DEPTH);

            int score = -InfiniteScore;
            int bestScore = -InfiniteScore;
            Move bestMove = Move.None;
            Span<Move> childPv = stackalloc Move[MAX_PLY];
            Position.StateInfo state = states[ply];
            bool inCheck = stack[ply].inCheck = position.IsCheck();

            selectiveDepth = Math.Max(selectiveDepth, ply + 1);

            CheckTime();

            if (Stop || position.IsDraw())
                return DrawScore;

            if (position.FiftyMoveCounter >= 3
                && alpha < DrawScore
                && position.HasRepeated(ply))
            {
                alpha = DrawScore;

                if (alpha >= beta)
                    return alpha;
            }

            if (ply >= MAX_PLY)
                return inCheck ? DrawScore : Evaluate(position);

            //Mate distance pruning
            alpha = Math.Max(MatedIn(ply), alpha);
            beta = Math.Min(MateIn(ply + 1), beta);
            if (alpha >= beta)
                return alpha;

            //Transposition table lookup
            Key positionKey = position.PositionKey;
            ref TranspositionTable.Entry ttentry = ref Engine.TranspositionTable.ProbeTable(positionKey, out bool ttHit);
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
                evaluation = stack[ply].evaluation = -InfiniteScore;
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
            }

            MovePicker movePicker = new(position, ttMove, butterflyTable, stackalloc MoveScore[MoveGenerators.MaxMoveCount]);
            int moveCount = 0;
            int nextPly = ply + 1;
            foreach (Move move in movePicker)
            {
                if (!position.IsLegal(move))
                    continue;

                moveCount++;
                stack[ply].currentMove = move;
                Piece piece = position.PieceAt(move.Origin());
                bool givesCheck = position.GivesCheck(move);
                bool isCapture = position.IsCapture(move);

                int newDepth = depth - 1;

                if (ply < RootDepth * 2)
                    newDepth += Extensions(depth, givesCheck);
               
                NodeCount++;
                position.MakeMove(move, state, givesCheck);

                int reduction = Reductions(moveCount, depth);

                //LMR
                if (depth > 2
                    && moveCount > 1
                    && (!isCapture))
                {
                    score = -ZWSearch(stack, nextPly, -(alpha + 1), newDepth - reduction);
                    
                    if(score > alpha)
                    {
                        score = -ZWSearch(stack, nextPly, -(alpha + 1), newDepth);
                    }
                }
                else if (moveCount > 1)
                {
                    score = -ZWSearch(stack, nextPly, -(alpha + 1), newDepth);
                }

                if (moveCount == 1 || (score > alpha))
                {
                    childPv[0] = Move.None;
                    score = -PVSearch(stack, nextPly, -beta, -alpha, newDepth, childPv);
                }

                position.Takeback(move);

                if (Stop)
                    return 0;

                Debug.Assert(score > -InfiniteScore && score < InfiniteScore);

                if (score > bestScore)
                {
                    bestScore = score;

                    if (score > alpha)
                    {
                        bestMove = move;

                        UpdatePV(pv, move, childPv);

                        if (score >= beta) //cutoff
                        {
                            if(!position.IsCapture(bestMove))
                                butterflyTable.GetReference((int)position.ActiveColor, (int)move.OriginDestination()) += depth * depth;
                            break;
                        }
                        else
                        {
                            alpha = score;

                            if (depth > 1
                                && depth < 6
                                && beta < ExpectedWin
                                && score > ExpectedLoss)
                                depth -= 1;

                            Debug.Assert(depth > 0);
                        }
                    }
                }
            }

            //If there is no legal moves in the position we are either mated or its stealmate
            if (moveCount == 0)
            {
                Debug.Assert(MoveGenerators.Legal.Generate(position, stackalloc MoveScore[MoveGenerators.MaxMoveCount]).Length == 0);

                bestScore = inCheck ? MatedIn(ply)
                                    : DrawScore;
            }

            Bound boundType = bestScore >= beta ? Bound.Lower //If a beta cut-off happened then this node is valued higher then the current eval
                                  : bestMove != Move.None ? Bound.Exact // Atleast one move raised alpha
                                                          : Bound.Upper; // This is an all node

            ttentry.Save(positionKey, true, bestMove, depth, boundType, bestScore);

            return bestScore;
        }

        private int ZWSearch(Span<SearchStack> stack, int ply, int alpha, int depth)
        {
            int beta = alpha + 1;

            //If remainingDepth is equal or zero dive into Quisence Search
            if (depth <= 0)
                return QuiescenceSearch(false, stack, ply, alpha, beta);

            //Check if the alpha and beta values are within acceptable values
            Debug.Assert(-InfiniteScore <= alpha);
            Debug.Assert(alpha < beta);
            Debug.Assert(beta <= InfiniteScore);

            //Check to see if depth values are in allowed range
            Debug.Assert(depth > 0 && depth < MAX_DEPTH);

            int score = -InfiniteScore;
            int bestScore = -InfiniteScore;
            Move bestMove = Move.None;
            Span<Move> childPv = stackalloc Move[MAX_PLY];
            Position.StateInfo state = states[ply];
            bool inCheck = stack[ply].inCheck = position.IsCheck();

            CheckTime();

            if (Stop || position.IsDraw())
                return DrawScore;

            if (position.FiftyMoveCounter >= 3
                && alpha < DrawScore
                && position.HasRepeated(ply))
            {
                alpha = DrawScore;

                if (alpha >= beta)
                    return alpha;
            }

            if (ply >= MAX_PLY)
                return inCheck ? DrawScore : Evaluate(position);

            //Mate distance pruning
            alpha = Math.Max(MatedIn(ply), alpha);
            beta = Math.Min(MateIn(ply + 1), beta);
            if (alpha >= beta)
                return alpha;

            //Transposition table lookup
            Key positionKey = position.PositionKey;
            ref TranspositionTable.Entry ttentry = ref Engine.TranspositionTable.ProbeTable(positionKey, out bool ttHit);

            stack[ply].ttHit = ttHit;

            int ttEval = ttHit ? ttentry.Evaluation : 0;
            Move ttMove = ttHit ? ttentry.Move : Move.None;
            bool ttCapture = ttMove != Move.None && position.IsCapture(ttMove);

            int evaluation = 0;

            if (!inCheck) 
            {
                if (ttHit)
                {
                    evaluation = stack[ply].evaluation = ttentry.Evaluation;
                }
                else
                {
                    evaluation = stack[ply].evaluation = Evaluate(position);
                }

                //Futility Pruning
                if (depth < 5
                    && evaluation >= beta + (2 * Pieces.QueenValue) // If we are somehow two queens up than expected most likely this position wont be played
                    && evaluation < MateInMaxPly - 1) //Dont return unproven mates 
                    return evaluation;

                //Null move reduction
                if (stack[ply - 1].currentMove != Move.Null
                    && evaluation >= beta)
                {
                    int R = (depth / 4) + 3;

                    stack[ply].currentMove = Move.Null;

                    position.MakeNullMove(state);

                    int nullScore = -ZWSearch(stack, ply + 1, -beta, depth - R);

                    position.TakebackNullMove();

                    if (nullScore >= beta)
                    {
                        depth -= 4;

                        if (depth <= 0)
                            return QuiescenceSearch(false, stack, ply, alpha, beta);
                    }
                }
            }
            else
                evaluation = stack[ply].evaluation = -InfiniteScore;

            MovePicker movePicker = new(position, ttMove, butterflyTable, stackalloc MoveScore[MoveGenerators.MaxMoveCount]);
            int moveCount = 0;
            int nextPly = ply + 1;
            foreach (Move move in movePicker)
            {
                if (!position.IsLegal(move))
                    continue;

                moveCount++;
                stack[ply].currentMove = move;
                Piece piece = position.PieceAt(move.Origin());
                bool givesCheck = position.GivesCheck(move);
                bool isCapture = position.IsCapture(move);

                int newDepth = depth - 1;

                if (ply < RootDepth * 2)
                    newDepth += Extensions(depth, givesCheck);

                NodeCount++;
                position.MakeMove(move, state, givesCheck);

                int reduction = Reductions(moveCount, depth);

                //LMR
                if (depth > 2
                    && moveCount > 1
                    && (!isCapture))
                {
                    score = -ZWSearch(stack, nextPly, -(alpha + 1), newDepth - reduction);

                    if (score > alpha)
                    {
                        score = -ZWSearch(stack, nextPly, -(alpha + 1), newDepth);
                    }
                }
                else 
                {
                    score = -ZWSearch(stack, nextPly, -(alpha + 1), newDepth);
                }

                position.Takeback(move);

                if (Stop)
                    return 0;

                Debug.Assert(score > -InfiniteScore && score < InfiniteScore);

                if (score > bestScore)
                {
                    bestScore = score;

                    if (score > alpha)
                    {
                        bestMove = move;

                        if (score >= beta) //cutoff
                        {
                            if (!position.IsCapture(bestMove))
                                butterflyTable.GetReference((int)position.ActiveColor, (int)move.OriginDestination()) += depth * depth;
                            break;
                        }
                        else
                        {
                            alpha = score;

                            if (depth > 1
                                && depth < 6
                                && beta < ExpectedWin
                                && score > ExpectedLoss)
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
                                    : DrawScore;
            }

            ttentry.Save(positionKey, false, bestMove, depth,
                bestScore >= beta ? Bound.Lower
                                  : Bound.Upper, bestScore);

            return bestScore;
        }

        private int QuiescenceSearch(bool pvNode, Span<SearchStack> stack, int ply, int alpha, int beta, int depth = 0, Span<Move> pv = default)
        {
            //Check if the alpha and beta values are within acceptable values
            Debug.Assert(-InfiniteScore <= alpha && alpha < beta && beta <= InfiniteScore);
            //This is a pv node or we are in a aspiration search
            Debug.Assert(pvNode || alpha == beta - 1);
            //Depth is negative
            Debug.Assert(depth <= 0 && depth > TTOffset);

            //Check for aborted search or draws
            if (Stop
                || position.IsDraw())
                return DrawScore;

            //Check to see if this position repeated during search
            //If yes we can claim this position as Draw
            if (position.FiftyMoveCounter >= 3
                && alpha < DrawScore
                && position.HasRepeated(ply))
            {
                alpha = DrawScore;
                if (alpha >= beta)
                    return alpha;
            }

            //Mate distance pruning
            alpha = Math.Max(MatedIn(ply), alpha);
            beta = Math.Min(MateIn(ply), beta);

            if (alpha >= beta)
                return alpha;

            Span<Move> childPV = stackalloc Move[MAX_PLY];
            Position.StateInfo state = states[ply];
            int bestValue = -InfiniteScore;
            Move bestMove = Move.None;
            bool inCheck = stack[ply].inCheck = position.IsCheck();

            CheckTime();

            if (pvNode)
                pv[0] = Move.None;

            if (ply >= MAX_PLY)
                return inCheck ? DrawScore : Evaluate(position);

            int ttDepth = (inCheck || depth >= QSChecks) ? QSChecks : QSNoChecks;


            //Transposition table lookup
            Key positionKey = position.PositionKey;
            ref TranspositionTable.Entry ttentry = ref Engine.TranspositionTable.ProbeTable(positionKey, out bool ttHit);
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
            if (!pvNode
                && ttHit
                && ttentry.Depth >= ttDepth
                && (ttentry.BoundType & (ttScore >= beta ? Bound.Lower : Bound.Upper)) != 0)
                return ttScore;

            //Static evaluation
            if (inCheck)
            {
                stack[ply].evaluation = 0;
                bestValue = -InfiniteScore;
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
                        ttentry.Save(positionKey, pvNode, Move.None, 0, Bound.Lower, stack[ply].evaluation);

                    return bestValue;
                }

                if (pvNode && bestValue > alpha)
                    alpha = bestValue;
            }

            MovePicker movePicker = new(position, ttMove, butterflyTable, stack[ply - 1].currentMove.Destination(), stackalloc MoveScore[MoveGenerators.MaxMoveCount]);
            int moveCount = 0;
            foreach (Move move in movePicker)
            {
                if (!position.IsLegal(move))
                    continue;

                moveCount++;
                stack[ply].currentMove = move;
                bool givesCheck = position.GivesCheck(move);

                //If we are not in a desperate situation we can skip the moves that returns a negative Static Exchange Evaluation
                if (bestValue > ExpectedLoss)
                {
                    if (!position.StaticExchangeEvaluationGE(move, -Pieces.PawnValue / 2))
                        continue;
                }

                NodeCount++;
                position.MakeMove(move, state, givesCheck);

                if (pvNode)
                    childPV[0] = Move.None;

                int score = -QuiescenceSearch(pvNode, stack, ply + 1, -beta, -alpha, Math.Max(depth - 1, TTOffset + 1), childPV);

                position.Takeback(move);

                if (Stop)
                    return 0;

                Debug.Assert(-InfiniteScore < score && score < InfiniteScore);

                if (score > bestValue)
                {
                    bestValue = score;

                    if (score > alpha)
                    {
                        bestMove = move;

                        if (pvNode)
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

            ttentry.Save(positionKey, pvNode, bestMove, ttDepth,
                bestValue >= beta ? Bound.Lower
                                 : pvNode && bestMove != Move.None ? Bound.Exact
                                                                   : Bound.Upper, bestValue);

            return bestValue;
        }

        private static void CheckTime()
        {
            if (SearchLimits.RequiresTimeManagement() && Timer.Elapsed() > Timer.MaxTime)
                Stop = true;
        }

        private static void UpdatePV(Span<Move> pv, Move move, Span<Move> childPv)
        {
            int i = 0, j = 0;
            pv[i++] = move;
            if (childPv.Length > 0)
                while (childPv[j] != Move.None)
                {
                    pv[i++] = childPv[j++];
                }

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