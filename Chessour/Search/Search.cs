using Chessour.Evaluation;
using Chessour.MoveGeneration;
using static Chessour.Engine;
using static Chessour.Evaluation.ValueConstants;
using static Chessour.Search.DepthConstants;

namespace Chessour.Search;

internal partial class SearchThread
{
    public Depth CompletedDepth { get; protected set; }
    public Depth SelectiveDepth { get; protected set; }
    public ulong NodeCount { get; protected set; }
    public bool Searching { get; protected set; }

    public void ResetSearchStats()
    {
        SelectiveDepth = CompletedDepth = 0;
        NodeCount = 0;
    }

    public void SetPosition(Position position)
    {
        rootMoves.Clear();

        this.position.Set(position, rootState);

        MoveList moves = new(this.position, (stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT]));
        foreach (Move m in moves)
            rootMoves.Add(new RootMove(m));
    }

    protected ulong Perft(int depth, int ply = 0)
    {
        ulong branchNodes, nodes = 0;

        var state = states[ply];

        foreach (Move m in new MoveList(position, stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT]))
        {
            if (ply == 0 && depth <= 1)
                nodes += branchNodes = 1;

            else
            {
                position.MakeMove(m, state);

                nodes += branchNodes = depth == 2 ? (ulong)new MoveList(position, stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT]).Count
                                                  : Perft(depth - 1, ply + 1);

                position.Takeback(m);
            }

            if (ply == 0)
                Console.WriteLine($"{m}: {branchNodes}");
        }

        return nodes;
    }

    protected void Search()
    {       
        Depth rootDepth = 0;

        Value bestValue = -Value_INF;
        Value alpha = -Value_INF;
        Value delta = -Value_INF;
        Value beta = Value_INF;

        Span<SearchStack> stacks = stackalloc SearchStack[MAX_PLY];

        while (++rootDepth < DepthConstants.Max
            && !Stop
            && !(SearchLimits.depth > 0 && rootDepth > SearchLimits.depth))
        {
            foreach (var rm in rootMoves)
                rm.PreviousScore = rm.Score;

            SelectiveDepth = 0;

            int failHighCount = 0;
            while (true)
            {
                bestValue = Search(NodeType.Root, stacks, alpha, beta, 0, rootDepth);

                rootMoves.Sort();

                if (Stop)
                    break;

                if (this as MasterThread != null
                    && (bestValue <= alpha || bestValue >= beta)
                    && Time.Elapsed > 3000)
                    Console.WriteLine(UCI.PV(this, rootDepth));

                if (bestValue <= alpha) //fail low
                {
                    beta = (alpha + beta) / 2;
                    alpha = Math.Max(bestValue - delta, -Value_INF);

                    failHighCount = 0;
                }
                else if (bestValue >= beta) //fail high
                {
                    beta = Math.Min(bestValue + delta, Value_INF);
                    failHighCount++;
                }
                else
                    break;

                delta += delta / 4;

                Debug.Assert(alpha >= -Value_INF && beta <= Value_INF);
            }

            if (this as MasterThread != null
                && (!Stop || Time.Elapsed > 3000))
                Console.WriteLine(UCI.PV(this, rootDepth));                    

            if (!Stop)
                CompletedDepth = rootDepth;

            if (SearchLimits.mate > 0
                && bestValue >= Value_Mate
                && Value_Mate- bestValue >= 2 * SearchLimits.mate)
                Stop = true;

            if (SearchLimits.UseTimeManagement()
                && !Stop)
            {
                if (Time.Elapsed > Time.OptimumTime)
                    Stop = true;
            }

            Debug.Assert(alpha >= -Value_INF && beta <= Value_INF);
        }
    }

    private Value Search(NodeType nodeType, Span<SearchStack> ss, Value alpha, Value beta, int ply, Depth depth, Span<Move> pv = default)
    {
        bool root = nodeType == NodeType.Root;
        bool pvNode = nodeType != NodeType.NonPV;

        if (depth <= 0)
            return QSearch(nodeType, ss, alpha, beta, ply, pv);

        NodeCount++;


        if (!root && position.IsDraw())
            return Value_Draw;


        Debug.Assert(-Value_INF <= alpha && alpha < beta && beta <= Value_INF);
        Debug.Assert(pvNode || (alpha == beta - 1));
        Debug.Assert(depth > 0 && depth < MAX_PLY);

        Span<Move> childPv = stackalloc Move[MAX_PLY];
        Position.StateInfo state = states[ply];

        ref SearchStack stack = ref ss[ply];

        Value value = -Value_INF;
        Value bestValue = -Value_INF;
        Value evaluation = -Value_INF;
        Move bestMove = Move.None;
        int moveCount = 0;

        stack.inCheck = position.IsCheck();

        CheckTime();

        if (pvNode && SelectiveDepth < ply + 1)
            SelectiveDepth = ply + 1;

        if (!root)
        {
            if (Stop)
                return 0;

            if (ply >= MAX_PLY)
                return ss[ply].inCheck ? Value_Draw : Evaluator.Evaluate(position);

            //Mate distance pruning
            alpha = Math.Max(MatedIn(ply), alpha);
            beta = Math.Min(MateIn(ply + 1), beta);
            if (alpha >= beta)
                return alpha;
        }


        //Transposition table lookup
        Key positionKey = position.ZobristKey;

        ref TranspositionTable.Entry ttentry = ref TTTable.ProbeTT(positionKey, out stack.ttHit);
        Value ttValue = stack.ttHit ? ttentry.Evaluation : 0;
        Move ttMove = root ? rootMoves[0].pv[0]
                            : stack.ttHit ? ttentry.Move
                                            : Move.None;
        bool ttCapture = ttMove != Move.None && position.IsCapture(ttMove);

        if (stack.inCheck)
        {
            evaluation = stack.staticEval = -Value_INF;
            goto movesloop;
        }
        else if (stack.ttHit)
        {
            evaluation = stack.staticEval = ttentry.Evaluation;          
        }
        else
        {
            evaluation = stack.staticEval = Evaluator.Evaluate(position);
        }

        //Static null move
        if (depth < 5
            && !pvNode
            && evaluation >= beta
            && evaluation < KnownWin + 1)
            return evaluation;

        //Null move search
        if(!pvNode
            && ss[ply - 1].currentMove != Move.Null
            && depth > 2
            && evaluation >= beta)
        {
            Depth reduction = 2;
            if (depth > 6) 
                reduction++;
            
            ss[ply].currentMove = Move.Null;

            position.MakeNullMove(state);
            Value nullValue = -Search(NodeType.NonPV, ss, -(alpha + 1), -alpha, ply + 1, depth - reduction - 1, pv);
            position.TakebackNullMove();

            if (Stop)
                return 0;

            if(nullValue >= beta)
            {
                return nullValue;
            }
        }

        movesloop:

        MovePicker mp = new(position, ttMove, stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT]);
        for (Move move; (move = mp.NextMove()) != Move.None;)
        {
            if (root && !rootMoves.Contains(move))
                continue;

            if (!root && !position.IsLegal(move))
                continue;

            moveCount++;
            ss[ply].currentMove = move;
            bool givesCheck = position.GivesCheck(move);

            if (pvNode)
                childPv[0] = Move.None;

            position.MakeMove(move, states[ply], givesCheck);

            if (!pvNode || moveCount > 1)
            {
                value = -Search(NodeType.NonPV, ss, -(alpha + 1), -alpha, ply + 1, depth - 1);
            }

            if (pvNode
                && (moveCount == 1
                || value > alpha
                    && (root || value < beta)))
            {
                childPv[0] = Move.None;

                value = -Search(NodeType.PV, ss, -beta, -alpha, ply + 1, depth - 1, childPv);
            }

            position.Takeback(move);

            if (Stop)
                return 0;

            Debug.Assert(value > -Value_INF && value < Value_INF);

            if (root)
            {
                RootMove rm = rootMoves.Find(move)!;

                if (moveCount == 1 || value > alpha)
                {
                    rm.Score = rm.UCIScore = value;
                    rm.SelectiveDepth = SelectiveDepth;

                    rm.BoundLower = rm.BoundUpper = false;

                    if (value >= beta)
                    {
                        rm.BoundLower = true;
                        rm.UCIScore = beta;
                    }
                    else if (value <= alpha)
                    {
                        rm.BoundUpper = true;
                        rm.UCIScore = alpha;
                    }
                    
                    rm.pv.Clear();
                    rm.pv.Add(move);
                    foreach(Move m in childPv)
                    {
                        if (m == Move.None) break;

                        rm.pv.Add(m);
                    }                   
                }
                else
                    rm.Score = -Value_INF;
            }

            if (value > bestValue)
            {
                bestValue = value;

                if (value > alpha)
                {
                    bestMove = move;

                    if (pvNode && !root)
                    {
                        UpdatePV(pv, move, childPv);
                    }

                    if (pvNode && value < beta)
                    {
                        alpha = value;
                   
                        if (depth > 1
                        && depth < 6
                        && beta < KnownWin
                        && alpha > -KnownWin)
                            depth -= 1;

                        Debug.Assert(depth > 0);
                    }
                    else
                    {
                        Debug.Assert(value >= beta);
                        break;
                    }
                }
            }
        }

        if (moveCount == 0)
        {
            bestValue = stack.inCheck ? MatedIn(ply)
                                         : Value_Draw;
        }

        ttentry.Save(positionKey, pvNode, bestMove, depth,
            bestValue >= beta ? Bound.Lower
                             : pvNode && bestMove != Move.None ? Bound.Exact
                                                               : Bound.Upper, bestValue);

        return bestValue;
    }

    private Value QSearch(NodeType nodeType, Span<SearchStack> ss, Value alpha, Value beta, int ply, Span<Move> pv, Depth depth = 0)
    {
        bool pvNode = nodeType != NodeType.NonPV;
        NodeCount++;

        if (position.IsDraw())
            return Value_Draw;

        Debug.Assert(nodeType != NodeType.Root); // We shouldn be entering qSearch at root node
        Debug.Assert(alpha >= -Value_INF && alpha < beta && beta <= Value_INF); //Value is not outside the defined bounds
        Debug.Assert(pvNode || alpha == beta - 1); //This is a pv node or we are in a aspiration search
        Debug.Assert(depth <= 0 && depth > DepthConstants.TTOffset); //Depth is negative

        Span<Move> childPV = stackalloc Move[MAX_PLY];
        Position.StateInfo state = states[ply];

        Value bestValue = -Value_INF;
        Move bestMove = Move.None;
        int moveCount = 0;

        ss[ply].inCheck = position.IsCheck();

        CheckTime();

        if (pvNode)
            pv[0] = Move.None;

        if (ply >= MAX_PLY)
            return ss[ply].inCheck ? Value_Draw : Evaluator.Evaluate(position);

        Depth ttDepth = ss[ply].inCheck || depth >= DepthConstants.QSChecks ? DepthConstants.QSChecks
                                                                            : DepthConstants.QSNoChecks;

        //Transposition table lookup
        Key positionKey = position.ZobristKey;
        ref TranspositionTable.Entry ttentry = ref TTTable.ProbeTT(positionKey, out ss[ply].ttHit);
        Value ttValue = ss[ply].ttHit ? ttentry.Evaluation : 0;
        Move ttMove = ss[ply].ttHit ? ttentry.Move
                                    : Move.None;

        if (!pvNode
            && ss[ply].ttHit
            && ttentry.Depth >= ttDepth
            && (ttentry.BoundType & (ttValue >= beta ? Bound.Lower : Bound.Upper)) != 0)
            return ttValue;

        //Static evaluation
        if (ss[ply].inCheck)
        {
            ss[ply].staticEval = 0;
            bestValue = -Value_INF;
        }
        else
        {
            if (ss[ply].ttHit)
            {
                if ((ss[ply].staticEval = bestValue = ttentry.Evaluation) == 0)
                    ss[ply].staticEval = bestValue = Evaluator.Evaluate(position);

                if (ttValue != 0
                    && (ttentry.BoundType & (ttValue > bestValue ? Bound.Lower : Bound.Upper)) != 0)
                    bestValue = ttValue;
            }
            else
                ss[ply].staticEval = bestValue = Evaluator.Evaluate(position);

            //Stand pat
            if (bestValue >= beta)
            {
                if (!ss[ply].ttHit)
                    ttentry.Save(positionKey, pvNode, Move.None, 0, Bound.Lower, ss[ply].staticEval);

                return bestValue;
            }

            if (pvNode && bestValue > alpha)
                alpha = bestValue;
        }

        MovePicker mp = new(position, ttMove, Square.a1, stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT]);
        for (Move move; (move = mp.NextMove()) != Move.None;)
        {
            if (!position.IsLegal(move))
                continue;

            moveCount++;
            ss[ply].currentMove = move;
            bool givesCheck = position.GivesCheck(move);

            //If we are not in a desperate situation we can skip the moves that returns a negative Static Exchange Evaluation
            if (bestValue > -20000
                && !position.SeeGe(move))
                continue;

            position.MakeMove(move, states[ply], givesCheck);

            Value value = -QSearch(nodeType, ss, -beta, -alpha, ply + 1, childPV, Math.Max(depth - 1, -6));

            position.Takeback(move);

            if (Stop)
                return 0;

            Debug.Assert(value > -Value_INF && value < Value_INF);

            if (value > bestValue)
            {
                bestValue = value;

                if (value > alpha)
                {
                    bestMove = move;

                    if (pvNode)
                        UpdatePV(pv, move, childPV);

                    if (pvNode && value < beta)
                        alpha = value;

                    else
                        break;   //Fail high                    
                }
            }
        }

        //After searching every evasion move if we have found no legal moves and we are in check we are mated
        if (ss[ply].inCheck && bestValue == -Value_INF)
        {
            Debug.Assert(new MoveList(position, stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT]).Count == 0);
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
        var elapsed = Time.Elapsed;
        
        if((SearchLimits.UseTimeManagement() && elapsed > Time.MaxTime))
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

    private enum NodeType
    {
        Root,
        PV,
        NonPV
    }

    private struct SearchStack
    {
        public bool inCheck;
        public bool ttHit;
        public Value staticEval;
        public Move currentMove;
    }
}
