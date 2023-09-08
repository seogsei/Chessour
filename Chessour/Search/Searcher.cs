using Chessour.Evaluation;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using static Chessour.Engine;
using static Chessour.Evaluation.Evaluator;
using static Chessour.Search.DepthConstants;

namespace Chessour.Search
{
    public enum NodeType { Root, PV, NonPV }

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
        
        private int completedDepth;
        private int selectiveDepth;

        public bool SendInfo { get; set; }
        public long NodeCount { get; set; }
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

        public void ResetSearchStats()
        {
            completedDepth = selectiveDepth = 0;
            RootDepth = 0;
            NodeCount = 0;
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

            foreach(Move move in MoveGenerators.Legal.Generate(position, stackalloc MoveScore[MoveGenerators.MAX_MOVE_COUNT]))
            {
                if (depth == 1)
                    totalNodes += branchNodes = 1;

                else
                {
                    position.MakeMove(move, state);
                    branchNodes = depth == 2 ? MoveGenerators.Legal.Generate(position, stackalloc MoveScore[MoveGenerators.MAX_MOVE_COUNT]).Length
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
            ResetSearchStats();

            Span<SearchStack> stack = stackalloc SearchStack[MAX_PLY];

            int bestValue = -Infinite;

            int maxPvIdx = Math.Min(1, rootMoves.Count);

            while(++RootDepth < MaxDepth
                && !Stop
                && !(SearchLimits.Depth > 0 && RootDepth > SearchLimits.Depth))
            {
                foreach (var rootMove in rootMoves)
                    rootMove.PreviousScore = rootMove.Score;

                for (int pvIdx = 0; pvIdx < maxPvIdx && !Stop; pvIdx++)
                {
                    selectiveDepth = 0;

                    int previousScore = rootMoves[pvIdx].PreviousScore;
                    int delta = Pieces.PawnValue + previousScore * previousScore / 4096;
                    int alpha =  Math.Max(previousScore - delta, -Infinite);
                    int beta = Math.Min(previousScore + delta, +Infinite);

                    while (true) 
                    {
                        //bestValue = NodeSearch(NodeType.Root, stack, 0, alpha, beta, RootDepth);
                        bestValue = RootSearch(stack, alpha, beta, RootDepth);

                        rootMoves.Sort(pvIdx);

                        if (Stop)
                            break;

                        if (bestValue <= alpha) //Fail low
                        {
                            alpha = Math.Max(bestValue - delta, -Infinite);
                        }
                        else if (bestValue >= beta) //Fail high
                        {
                            beta = Math.Min(bestValue + delta, Infinite);
                        }
                        else
                            break;

                        delta = beta - alpha;
                        Debug.Assert(-Infinite <= alpha && beta <= Infinite);
                    }

                    if (SendInfo
                        && (Stop || pvIdx + 1 == maxPvIdx))
                    {
                        UCI.SendPV(this, RootDepth);
                    }
                }

                if (!Stop)
                    completedDepth = RootDepth;

                if (SearchLimits.Mate > 0
                    && bestValue >= MateValue
                    && MateValue - bestValue >= 2 * SearchLimits.Mate)
                    Stop = true;

                if (SearchLimits.RequiresTimeManagement() && Timer.Elapsed() > Timer.OptimumTime)
                    Stop = true;
            }
        }

        private int RootSearch(Span<SearchStack> stack, int alpha, int beta, int depth)
        {
            //Check if the alpha and beta values are within acceptable values
            Debug.Assert(-Infinite <= alpha);
            Debug.Assert(alpha < beta);
            Debug.Assert(beta <= Infinite);

            //Check to see if depth values are in allowed range
            Debug.Assert(depth > 0 && depth < MAX_DEPTH);

            int score = -Infinite;
            int evaluation = -Infinite;
            Span<Move> childPv = stackalloc Move[MAX_PLY];
            Position.StateInfo state = states[0];
            bool inCheck = stack[0].inCheck = position.IsCheck();
            
            selectiveDepth = 1;

            if (inCheck)
                evaluation = stack[0].evaluation = -Infinite;
            else
                evaluation = stack[0].evaluation = Evaluate(position);

            int moveCount = 0;
            foreach (var rootMove in rootMoves)
            {
                Move move = rootMove.Move;

                moveCount++;
                stack[0].currentMove = move;
                Piece piece = position.PieceAt(move.OriginSquare());
                bool givesCheck = position.GivesCheck(move);
                bool isCapture = position.IsCapture(move);

                int newDepth = depth - 1;

                int extension = 0;

                if (givesCheck)
                    extension = 1;

                newDepth += extension;

                NodeCount++;
                position.MakeMove(move, state, givesCheck);

                if (moveCount > 1)
                {
                    int R = Reductions(move, moveCount, piece, isCapture, givesCheck);

                    score = -ZWSearch(stack, 1, -(alpha + 1), newDepth - R);
                }

                if (moveCount == 1 || (score > alpha))
                {
                    childPv[0] = Move.None;                   
                    score = -PVSearch( stack, 1, -beta, -alpha, newDepth, childPv);
                }

                position.Takeback(move);

                if (Stop)
                    return 0;

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
                        if (pvMove != Move.None)
                            rootMove.PV.Add(pvMove);
                        else
                            break;
                }
                else
                    rootMove.Score = -Infinite;

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
                return QuiescenceSearch(NodeType.PV, stack, ply, alpha, beta, 0, pv);

            //Check if the alpha and beta values are within acceptable values
            Debug.Assert(-Infinite <= alpha);
            Debug.Assert(alpha < beta);
            Debug.Assert(beta <= Infinite);

            //Check to see if depth values are in allowed range
            Debug.Assert(depth > 0 && depth < MAX_DEPTH);

            int score = -Infinite;
            int bestScore = -Infinite;
            Move bestMove = Move.None;
            Span<Move> childPv = stackalloc Move[MAX_PLY];
            Position.StateInfo state = states[ply];
            bool inCheck = stack[ply].inCheck = position.IsCheck();

            selectiveDepth = Math.Max(selectiveDepth, ply + 1);

            CheckTime();

            if (Stop || position.IsDraw())
                return DrawValue;

            if (position.FiftyMoveCounter >= 3
                && alpha < DrawValue
                && position.HasRepeated(ply))
            {
                alpha = DrawValue;

                if (alpha >= beta)
                    return alpha;
            }

            if (ply >= MAX_PLY)
                return inCheck ? DrawValue : Evaluate(position);

            //Mate distance pruning
            alpha = Math.Max(MatedIn(ply), alpha);
            beta = Math.Min(MateIn(ply + 1), beta);
            if (alpha >= beta)
                return alpha;

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
            }
          
            MovePicker movePicker = new(position, ttMove, stackalloc MoveScore[MoveGenerators.MAX_MOVE_COUNT]);
            int moveCount = 0;
            int nextPly = ply + 1;
            foreach (Move move in movePicker)
            {
                if (!position.IsLegal(move))
                    continue;

                moveCount++;
                stack[ply].currentMove = move;
                Piece piece = position.PieceAt(move.OriginSquare());
                bool givesCheck = position.GivesCheck(move);
                bool isCapture = position.IsCapture(move);

                int newDepth = depth - 1;

                int extensions = 0;
                if (ply < RootDepth * 2)
                {
                    if (givesCheck)
                        extensions = 1;
                }

                newDepth += extensions;

                NodeCount++;
                position.MakeMove(move, state, givesCheck);

                if (moveCount > 1)
                {
                    int R = Reductions(move, moveCount, piece, isCapture, givesCheck);

                    score = -ZWSearch(stack, nextPly, -(alpha + 1), newDepth - R);
                }

                if (moveCount == 1 || (score > alpha))
                {
                    childPv[0] = Move.None;
                    score = -PVSearch(stack, nextPly, -beta, -alpha, newDepth, childPv);
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

                        UpdatePV(pv, move, childPv);

                        if (score >= beta)
                            break;
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
                                    : DrawValue;
            }

            ttentry.Save(positionKey, true, bestMove, depth,
                bestScore >= beta ? Bound.Lower
                                  : bestMove != Move.None ? Bound.Exact
                                                          : Bound.Upper, bestScore);

            return bestScore;
        }

        private int ZWSearch(Span<SearchStack> stack, int ply, int alpha, int depth)
        {
            int beta = alpha + 1;

            //If remainingDepth is equal or zero dive into Quisence Search
            if (depth <= 0)
                return QuiescenceSearch(NodeType.NonPV, stack, ply, alpha, beta);

            //Check if the alpha and beta values are within acceptable values
            Debug.Assert(-Infinite <= alpha);
            Debug.Assert(alpha < beta);
            Debug.Assert(beta <= Infinite);

            //Check to see if depth values are in allowed range
            Debug.Assert(depth > 0 && depth < MAX_DEPTH);

            int score = -Infinite;
            int bestScore = -Infinite;
            Move bestMove = Move.None;
            Span<Move> childPv = stackalloc Move[MAX_PLY];
            Position.StateInfo state = states[ply];
            bool inCheck = stack[ply].inCheck = position.IsCheck();

            CheckTime();

            if (Stop || position.IsDraw())
                return DrawValue;

            if (position.FiftyMoveCounter >= 3
                && alpha < DrawValue
                && position.HasRepeated(ply))
            {
                alpha = DrawValue;

                if (alpha >= beta)
                    return alpha;
            }

            if (ply >= MAX_PLY)
                return inCheck ? DrawValue : Evaluate(position);

            //Mate distance pruning
            alpha = Math.Max(MatedIn(ply), alpha);
            beta = Math.Min(MateIn(ply + 1), beta);
            if (alpha >= beta)
                return alpha;

            //Transposition table lookup
            Key positionKey = position.PositionKey;
            ref TranspositionTable.Entry ttentry = ref TTTable.ProbeTable(positionKey, out bool ttHit);
            
            stack[ply].ttHit = ttHit;

            int ttEval = ttHit ? ttentry.Evaluation : 0;
            Move ttMove = ttHit ? ttentry.Move : Move.None;
            bool ttCapture = ttMove != Move.None && position.IsCapture(ttMove);

            int evaluation = 0;

            if (inCheck)
            {
                evaluation = stack[ply].evaluation = -Infinite;
                goto movesLoop;
            }

            if (ttHit)
            {
                evaluation = stack[ply].evaluation = ttentry.Evaluation;
            }
            else
            {
                evaluation = stack[ply].evaluation = Evaluate(position);
            }

            //Futility Pruning
            if (depth < 9
                && evaluation >= beta + 2 * Pieces.QueenValue
                && evaluation < ExpectedWin + 1)
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
                }
            }

            movesLoop:

            MovePicker movePicker = new(position, ttMove, stackalloc MoveScore[MoveGenerators.MAX_MOVE_COUNT]);
            int moveCount = 0;
            int nextPly = ply + 1;
            foreach (Move move in movePicker)
            {
                if (!position.IsLegal(move))
                    continue;

                moveCount++;
                stack[ply].currentMove = move;
                Piece piece = position.PieceAt(move.OriginSquare());
                bool givesCheck = position.GivesCheck(move);
                bool isCapture = position.IsCapture(move);

                int newDepth = depth - 1;

                int extensions = 0;

                if (ply < RootDepth * 2)
                {
                    if (givesCheck)
                        extensions = 1;
                }

                newDepth += extensions;

                NodeCount++;
                position.MakeMove(move, state, givesCheck);

                int R = Reductions(move, moveCount, piece, isCapture, givesCheck);

                score = -ZWSearch(stack, nextPly, -(alpha + 1), newDepth - R);

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

                        if (score >= beta)
                            break;
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
                                    : DrawValue;
            }

            ttentry.Save(positionKey, false, bestMove, depth,
                bestScore >= beta ? Bound.Lower
                                  : Bound.Upper, bestScore);

            return bestScore;
        }

        private int QuiescenceSearch(NodeType nodeType, Span<SearchStack> stack, int ply, int alpha, int beta, int depth = 0, Span<Move> pv = default)
        {
            bool IsPV() => nodeType != NodeType.NonPV;

            //Check if the alpha and beta values are within acceptable values
            Debug.Assert(-Infinite <= alpha && alpha < beta && beta <= Infinite);
            //This is a pv node or we are in a aspiration search
            Debug.Assert(IsPV() || alpha == beta - 1);
            //Depth is negative
            Debug.Assert(depth <= 0 && depth > TTOffset);

            //Check for aborted search or draws
            if (Stop
                || position.IsDraw())
                return DrawValue;

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
            
            //Mate distance pruning
            alpha = Math.Max(MatedIn(ply), alpha);
            beta = Math.Min(MateIn(ply), beta);

            if (alpha >= beta)
                return alpha;

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
                if (bestValue > ExpectedLoss)
                {
                    if (!position.StaticExchangeEvaluationGE(move, -Pieces.PawnValue / 2))
                        continue;
                }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Reductions(Move move, int moveCount, Piece piece, bool isCapture, bool givesCheck)
        {
            if (givesCheck)
                return 0;

            if (piece.TypeOf() == PieceType.Pawn)
                return 0;

            int R = 0;

            if (moveCount > 6)
                R++;

            return R;
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
            if(childPv.Length > 0)
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