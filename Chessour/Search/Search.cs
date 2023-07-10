using Chessour.Evaluation;
using static Chessour.Engine;
using static Chessour.Evaluation.ValueConstants;
using static Chessour.Search.DepthConstants;

namespace Chessour.Search;

internal partial class SearchThread
{
    public int CompletedDepth { get; protected set; }
    public int SelectiveDepth { get; protected set; }
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
        var moves = MoveGenerator.Generate(position, stackalloc MoveScore[256]);

        foreach (Move m in moves)
            rootMoves.Add(new RootMove(m));
    }

    public ulong Perft(int depth)
    {
        return Perft(depth, 0);

        ulong Perft(int depth, int distanceToRoot)
        {
            var state = states[distanceToRoot];
            bool leaf = depth == 2;

            ulong branchNodes, totalNodes = 0;
            foreach (Move move in MoveGenerator.Generate(position, stackalloc MoveScore[256]))
            {
                if (distanceToRoot == 0 && depth == 1)
                    totalNodes += branchNodes = 1;

                else
                {
                    position.MakeMove(move, state);
                    branchNodes = leaf ? (ulong)MoveGenerator.Generate(position, stackalloc MoveScore[256]).Length
                                       : Perft(depth - 1, distanceToRoot + 1);
                    totalNodes += branchNodes;
                    position.Takeback(move);
                }

                if (distanceToRoot == 0)
                    Console.WriteLine($"{UCI.ToLongAlgebraic(move)}: {branchNodes}");
            }

            return totalNodes;
        }
    }

    protected void Search()
    {
        int rootDepth = 0;

        int bestValue = -Infinite;
        int alpha = -Infinite;
        int delta = -Infinite;
        int beta = Infinite;

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

                if ((this as MasterThread) != null
                    && (bestValue <= alpha || bestValue >= beta)
                    && Engine.TimeManager.Elapsed > 3000)
                    Console.WriteLine(UCI.ParsePV(this, rootDepth));

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

            if ((this as MasterThread) != null
                && (!Stop || Engine.TimeManager.Elapsed > 3000))
                Console.WriteLine(UCI.ParsePV(this, rootDepth));

            if (!Stop)
                CompletedDepth = rootDepth;

            if (SearchLimits.mate > 0
                && bestValue >= Mate
                && Mate - bestValue >= 2 * SearchLimits.mate)
                Stop = true;

            if (SearchLimits.UseTimeManagement()
                && !Stop)
            {
                if (Engine.TimeManager.Elapsed > Engine.TimeManager.OptimumTime)
                    Stop = true;
            }

            Debug.Assert(alpha >= -Infinite && beta <= Infinite);
        }
    }

    private int Search(NodeType nodeType, Span<SearchStack> ss, int alphaValue, int betaValue, int distanceToRoot, int depth, Span<Move> pv = default)
    {
        bool root = nodeType == NodeType.Root;
        bool pvNode = nodeType != NodeType.NonPV;

        if (depth <= 0)
            return QSearch(nodeType, ss, alphaValue, betaValue, distanceToRoot, pv);

        NodeCount++;


        if (!root && position.IsDraw())
            return Draw;


        Debug.Assert(-Infinite <= alphaValue && alphaValue < betaValue && betaValue <= Infinite);
        Debug.Assert(pvNode || (alphaValue == betaValue - 1));
        Debug.Assert(depth > 0 && depth < MAX_PLY);

        Span<Move> childPv = stackalloc Move[MAX_PLY];
        Position.StateInfo state = states[distanceToRoot];

        ref SearchStack stack = ref ss[distanceToRoot];

        int value = -Infinite;
        int bestValue = -Infinite;
        int evaluation = -Infinite;
        Move bestMove = Move.None;
        int moveCount = 0;

        stack.inCheck = position.IsCheck();

        CheckTime();

        if (pvNode && SelectiveDepth < distanceToRoot + 1)
            SelectiveDepth = distanceToRoot + 1;

        if (!root)
        {
            if (Stop)
                return 0;

            if (distanceToRoot >= MAX_PLY)
                return ss[distanceToRoot].inCheck ? Draw : Evaluator.Evaluate(position);

            //Mate distance pruning
            alphaValue = Math.Max(MatedIn(distanceToRoot), alphaValue);
            betaValue = Math.Min(MateIn(distanceToRoot + 1), betaValue);
            if (alphaValue >= betaValue)
                return alphaValue;
        }


        //Transposition table lookup
        Key positionKey = position.ZobristKey;

        ref TranspositionTable.Entry ttentry = ref TTTable.ProbeTT(positionKey, out stack.ttHit);
        int ttValue = stack.ttHit ? ttentry.Evaluation : 0;
        Move ttMove = root ? rootMoves[0].pv[0]
                            : stack.ttHit ? ttentry.Move
                                            : Move.None;
        bool ttCapture = ttMove != Move.None && position.IsCapture(ttMove);

        if (stack.inCheck)
        {
            evaluation = stack.staticEval = -Infinite;
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
            && evaluation >= betaValue
            && evaluation < ExpectedWin + 1)
            return evaluation;

        //Null move search
        if (!pvNode
            && ss[distanceToRoot - 1].currentMove != Move.Null
            && depth > 2
            && evaluation >= betaValue)
        {
            int reduction = 2;
            if (depth > 6)
                reduction++;

            ss[distanceToRoot].currentMove = Move.Null;

            position.MakeNullMove(state);
            int nullValue = -Search(NodeType.NonPV, ss, -(alphaValue + 1), -alphaValue, distanceToRoot + 1, depth - reduction - 1, pv);
            position.TakebackNullMove();

            if (Stop)
                return 0;

            if (nullValue >= betaValue)
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
            ss[distanceToRoot].currentMove = move;
            bool givesCheck = position.GivesCheck(move);

            if (pvNode)
                childPv[0] = Move.None;

            position.MakeMove(move, states[distanceToRoot], givesCheck);

            if (!pvNode || moveCount > 1)
            {
                value = -Search(NodeType.NonPV, ss, -(alphaValue + 1), -alphaValue, distanceToRoot + 1, depth - 1);
            }

            if (pvNode
                && (moveCount == 1
                || (value > alphaValue
                    && (root || value < betaValue))))
            {
                childPv[0] = Move.None;

                value = -Search(NodeType.PV, ss, -betaValue, -alphaValue, distanceToRoot + 1, depth - 1, childPv);
            }

            position.Takeback(move);

            if (Stop)
                return 0;

            Debug.Assert(value > -Infinite && value < Infinite);

            if (root)
            {
                RootMove rm = rootMoves.Find(move)!;

                if (moveCount == 1 || value > alphaValue)
                {
                    rm.Score = rm.UCIScore = value;
                    rm.SelectiveDepth = SelectiveDepth;

                    rm.BoundLower = rm.BoundUpper = false;

                    if (value >= betaValue)
                    {
                        rm.BoundLower = true;
                        rm.UCIScore = betaValue;
                    }
                    else if (value <= alphaValue)
                    {
                        rm.BoundUpper = true;
                        rm.UCIScore = alphaValue;
                    }

                    rm.pv.Clear();
                    rm.pv.Add(move);
                    foreach (Move m in childPv)
                    {
                        if (m == Move.None) break;

                        rm.pv.Add(m);
                    }
                }
                else
                    rm.Score = -Infinite;
            }

            if (value > bestValue)
            {
                bestValue = value;

                if (value > alphaValue)
                {
                    bestMove = move;

                    if (pvNode && !root)
                    {
                        UpdatePV(pv, move, childPv);
                    }

                    if (pvNode && value < betaValue)
                    {
                        alphaValue = value;

                        if (depth > 1
                        && depth < 6
                        && betaValue < ExpectedWin
                        && alphaValue > -ExpectedWin)
                            depth -= 1;

                        Debug.Assert(depth > 0);
                    }
                    else
                    {
                        Debug.Assert(value >= betaValue);
                        break;
                    }
                }
            }
        }

        if (moveCount == 0)
        {
            bestValue = stack.inCheck ? MatedIn(distanceToRoot)
                                         : Draw;
        }

        ttentry.Save(positionKey, pvNode, bestMove, depth,
            bestValue >= betaValue ? Bound.Lower
                             : pvNode && bestMove != Move.None ? Bound.Exact
                                                               : Bound.Upper, bestValue);

        return bestValue;
    }

    private int QSearch(NodeType nodeType, Span<SearchStack> ss, int alphaValue, int betaValue, int distanceToRoot, Span<Move> pv, int depth = 0)
    {
        bool pvNode = nodeType != NodeType.NonPV;
        NodeCount++;

        if (position.IsDraw())
            return Draw;

        Debug.Assert(nodeType != NodeType.Root); // We shouldn be entering qSearch at root node
        Debug.Assert(alphaValue >= -Infinite && alphaValue < betaValue && betaValue <= Infinite); //Value is not outside the defined bounds
        Debug.Assert(pvNode || alphaValue == betaValue - 1); //This is a pv node or we are in a aspiration search
        Debug.Assert(depth <= 0 && depth > DepthConstants.TTOffset); //Depth is negative

        Span<Move> childPV = stackalloc Move[MAX_PLY];
        Position.StateInfo state = states[distanceToRoot];

        int bestValue = -Infinite;
        Move bestMove = Move.None;
        int moveCount = 0;

        ss[distanceToRoot].inCheck = position.IsCheck();

        CheckTime();

        if (pvNode)
            pv[0] = Move.None;

        if (distanceToRoot >= MAX_PLY)
            return ss[distanceToRoot].inCheck ? Draw : Evaluator.Evaluate(position);

        int ttDepth = ss[distanceToRoot].inCheck || depth >= DepthConstants.QSChecks ? DepthConstants.QSChecks
                                                                          : DepthConstants.QSNoChecks;

        //Transposition table lookup
        Key positionKey = position.ZobristKey;
        ref TranspositionTable.Entry ttentry = ref TTTable.ProbeTT(positionKey, out ss[distanceToRoot].ttHit);
            int ttValue = ss[distanceToRoot].ttHit ? ttentry.Evaluation : 0;
        Move ttMove = ss[distanceToRoot].ttHit ? ttentry.Move
                                    : Move.None;

        if (!pvNode
            && ss[distanceToRoot].ttHit
            && ttentry.Depth >= ttDepth
            && (ttentry.BoundType & (ttValue >= betaValue ? Bound.Lower : Bound.Upper)) != 0)
            return ttValue;

        //Static evaluation
        if (ss[distanceToRoot].inCheck)
        {
            ss[distanceToRoot].staticEval = 0;
            bestValue = -Infinite;
        }
        else
        {
            if (ss[distanceToRoot].ttHit)
            {
                if ((ss[distanceToRoot].staticEval = bestValue = ttentry.Evaluation) == 0)
                    ss[distanceToRoot].staticEval = bestValue = Evaluator.Evaluate(position);

                if (ttValue != 0
                    && (ttentry.BoundType & (ttValue > bestValue ? Bound.Lower : Bound.Upper)) != 0)
                    bestValue = ttValue;
            }
            else
                ss[distanceToRoot].staticEval = bestValue = Evaluator.Evaluate(position);

            //Stand pat
            if (bestValue >= betaValue)
            {
                if (!ss[distanceToRoot].ttHit)
                    ttentry.Save(positionKey, pvNode, Move.None, 0, Bound.Lower, ss[distanceToRoot].staticEval);

                return bestValue;
            }

            if (pvNode && bestValue > alphaValue)
                alphaValue = bestValue;
        }

        MovePicker mp = new(position, ttMove, Square.a1, stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT]);
        for (Move move; (move = mp.NextMove()) != Move.None;)
        {
            if (!position.IsLegal(move))
                continue;

            moveCount++;
            ss[distanceToRoot].currentMove = move;
            bool givesCheck = position.GivesCheck(move);

            //If we are not in a desperate situation we can skip the moves that returns a negative Static Exchange Evaluation
            if (bestValue > -20000
                && !position.SeeGe(move))
                continue;

            position.MakeMove(move, states[distanceToRoot], givesCheck);

            int value = -QSearch(nodeType, ss, -betaValue, -alphaValue, distanceToRoot + 1, childPV, Math.Max(depth - 1, -6));

            position.Takeback(move);

            if (Stop)
                return 0;

            Debug.Assert(value > -Infinite && value < Infinite);

            if (value > bestValue)
            {
                bestValue = value;

                if (value > alphaValue)
                {
                    bestMove = move;

                    if (pvNode)
                        UpdatePV(pv, move, childPV);

                    if (pvNode && value < betaValue)
                        alphaValue = value;

                    else
                        break;   //Fail high                    
                }
            }
        }

        //After searching every evasion move if we have found no legal moves and we are in check we are mated
        if (ss[distanceToRoot].inCheck && bestValue == -Infinite)
        {
            Debug.Assert(MoveGenerator.Generate(position, stackalloc MoveScore[256]).Length == 0);
            return MatedIn(distanceToRoot);
        }

        ttentry.Save(positionKey, pvNode, bestMove, ttDepth,
            bestValue >= betaValue ? Bound.Lower
                             : pvNode && bestMove != Move.None ? Bound.Exact
                                                                : Bound.Upper, bestValue);

        return bestValue;
    }

    private static void CheckTime()
    {
        var elapsed = Engine.TimeManager.Elapsed;

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
        public int staticEval;
        public Move currentMove;
    }
}