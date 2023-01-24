using static Chessour.Position;

namespace Chessour
{
    class Search
    {
        public readonly RootMoves rootMoves;
        public SearchStats stats;
        public UCI.SearchLimits limits;
        public volatile bool stop;
        public volatile bool sendInfo;

        readonly Position rootPosition;
        readonly StateInfo rootState;
        readonly StateInfo[] states;
     
        public Search()
        {
            rootPosition = new(UCI.StartFEN, rootState = new());
            rootMoves = new();

            states = new StateInfo[MAX_PLY];
            for (int i = 0; i < MAX_PLY; i++)
            {
                states[i] = new();
            }
        }

        enum NodeType
        {
            Root,
            PV,
            NonPV
        }

        public void SetUpSearchParameters(Position position, in UCI.SearchLimits limits)
        {
            rootPosition.Set(position, rootState);
            rootMoves.Clear();

            this.limits = limits;

            MoveList moves = new(rootPosition, stackalloc MoveScore[MAX_MOVE_COUNT]);
            foreach (Move m in moves)
                if (limits.searchMoves is null || limits.searchMoves.Contains(m))
                    rootMoves.Add(new RootMove(m));
        }

        public void StartSearch(Position position, in UCI.SearchLimits limits)
        {
            SetUpSearchParameters(position, limits);
            StartSearch();
        }

        public void StartSearch()
        {
            stats = new SearchStats();
            
            if (limits.perft > 0)
            {
                stats.nodeCount = Perft(limits.perft);
                return;
            }

            Depth iterativeDepth = 0;
            Value bestValue = Value.Min;
            Value alpha = Value.Min;
            Value delta = Value.Min;
            Value beta = Value.Max;

            Span<SearchStack> stacks = stackalloc SearchStack[MAX_PLY];


            while(++iterativeDepth < Depth.Max
                && !stop
                && !(limits.depth > 0 && iterativeDepth > limits.depth))
            {
                foreach (var rm in rootMoves)
                    rm.previousScore = rm.score;

                stats.selectiveDepth = 0;

                int failHighCount = 0;
                while (true)
                {
                    bestValue = SearchMain(NodeType.Root, stacks, alpha, beta, 0, iterativeDepth);

                    rootMoves.Sort();

                    if (stop)
                        break;

                    if (bestValue <= alpha) //fail low
                    {
                        beta = (Value)((int)(alpha + (int)beta) / 2);
                        alpha = Max(bestValue - (int)delta, Value.Min);

                        failHighCount = 0;
                    }
                    else if (bestValue >= beta) //fail high
                    {
                        beta = Min(bestValue + (int)delta, Value.Max);
                        failHighCount++;
                    }
                    else 
                        break;

                    delta += (int)delta / 4;

                    Debug.Assert(alpha >= Value.Min && beta <= Value.Max);
                }

                rootMoves.Sort();

                if (sendInfo)
                    Console.WriteLine($"info depth {(int)iterativeDepth} seldepth {stats.selectiveDepth} score {UCI.ToString(bestValue)} nodes {stats.nodeCount} qnodes {stats.qNodeCount} pv {UCI.ParsePV(rootMoves[0].pv)}");

                if (!stop)
                    stats.completedDepth = iterativeDepth;

                if(limits.mate > 0
                    && bestValue >= Value.MateInMaxPly
                    && Value.Mate - bestValue >= 2 * limits.mate)
                        stop = true;

                Debug.Assert(alpha >= Value.Min && beta <= Value.Max);                           
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

        private ulong Perft(int depth, int ply = 0)
        {
            ulong branchNodes, nodes = 0;

            var state = states[ply];

            foreach (Move m in new MoveList(rootPosition, stackalloc MoveScore[MAX_MOVE_COUNT]))
            {
                if (ply == 0 && depth <= 1)
                    nodes += branchNodes = 1;

                else
                {
                    rootPosition.MakeMove(m, state);

                    nodes += branchNodes = depth == 2 ? (ulong)new MoveList(rootPosition, stackalloc MoveScore[MAX_MOVE_COUNT]).Count
                                                      : Perft(depth - 1, ply + 1);

                    rootPosition.Takeback(m);
                }

                if (ply == 0)
                    Console.WriteLine($"{m.LongAlgebraicNotation()}: {branchNodes}");
            }

            return nodes;
        }

        private Value SearchMain(NodeType nodeType, Span<SearchStack> ss, Value alpha, Value beta, int ply, Depth depth, Span<Move> pv = default)
        {
            bool root = nodeType == NodeType.Root;
            bool pvNode = nodeType != NodeType.NonPV;

            if (depth <= 0)
                return QSearch(nodeType, ss, alpha, beta, ply, pv);


            Span<Move> childPv = stackalloc Move[MAX_PLY];

            stats.nodeCount++;
            ss[ply].inCheck = rootPosition.IsCheck();
            Value bestValue = Value.Min;
            Move bestMove = Move.None;
            int moveCount = 0;

            if (pvNode && stats.selectiveDepth < (Depth)(ply + 1))
                stats.selectiveDepth = (Depth)ply + 1;

            if (!root)
            {
                //Mate distance pruning
                alpha = Max(MatedIn(ply), alpha);
                beta = Min(MateIn(ply + 1), beta);
                if (alpha >= beta)
                    return alpha;
            }


            //Transposition table lookup
            Key positionKey = rootPosition.ZobristKey;
            ref TranspositionTable.Entry ttentry = ref Engine.TTTable.ProbeTT(positionKey, out ss[ply].ttHit);
            Value ttValue = ss[ply].ttHit ? ttentry.Evaluation : Value.Zero;
            Move ttMove = root ? rootMoves[0].pv[0]
                                : ss[ply].ttHit ? ttentry.Move
                                                : Move.None;


            MovePicker mp = new(rootPosition, ttMove, stackalloc MoveScore[MAX_MOVE_COUNT]);
            for(Move move; ((move = mp.NextMove()) != Move.None);)
            {
                if (root && !rootMoves.Contains(move))
                    continue;

                if (!root && !rootPosition.IsLegal(move))
                    continue;

                moveCount++;
                bool givesCheck = rootPosition.GivesCheck(move);

                if (pvNode)
                    childPv[0] = Move.None;

                rootPosition.MakeMove(move, states[ply], givesCheck);

                //For any node outside of the pv we are doing aspiration searches to prove our pv line is just fine
                //For more information read PVS and Aspiration section in : https://www.chessprogramming.org/Principal_Variation_Search
                Value value = Value.Min;
                if (!pvNode || moveCount > 1)
                {
                    value = SearchMain(NodeType.NonPV, ss, (alpha + 1).Negate(), alpha.Negate(), ply + 1, depth - 1).Negate();
                }

                if(pvNode 
                    && moveCount == 1
                    || (value > alpha
                        && (root || value < beta)))
                {
                    childPv[0] = Move.None;

                    value = SearchMain(NodeType.PV, ss, beta.Negate(), alpha.Negate(), ply + 1, depth - 1, childPv).Negate();
                }

                rootPosition.Takeback(move);

                if (stop)
                    return 0;

                Debug.Assert(value > Value.Min && value < Value.Max);

                if (root)
                {
                    RootMove rm = rootMoves.Find(move) ?? throw new Exception();

                    if (moveCount == 1 || value > alpha)
                    {
                        rm.score = rm.uciScore = value;
                        rm.selDepth = stats.selectiveDepth;

                        rm.boundLower = rm.boundUpper = false;

                        if(value >= beta)
                        {
                            rm.boundLower = true;
                            rm.uciScore = beta;
                        }
                        else if(value <= alpha)
                        {
                            rm.boundUpper = true;
                            rm.uciScore = alpha;
                        }

                        Array.Clear(rm.pv, 1, rm.pv.Length - 1);
                        UpdatePV(rm.pv, move, childPv);
                    }
                    else
                        rm.score = Value.Min;
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

                            //Early on the search we want to keep looking forward in our tt move as it can be very volatile                            
                            if (depth > (Depth)1
                            && depth < (Depth)6
                            && beta < Value.KnownWin
                            && alpha > Value.KnownLoss)
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

            if(moveCount == 0)
            {
                bestValue = ss[ply].inCheck ? MatedIn(ply)
                                                : Value.Draw;
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

            Debug.Assert(nodeType != NodeType.Root); // We shouldn be entering qSearch at root node
            Debug.Assert(alpha >= Value.Min && alpha < beta && beta <= Value.Max); //Value is not outside the defined bounds
            Debug.Assert(pvNode || (alpha == beta - 1)); //This is a pv node or we are in a aspiration search
            Debug.Assert(depth <= 0); //Depth is not outside defined bounds


            stats.qNodeCount++;
            stats.nodeCount++;
            ss[ply].inCheck = rootPosition.IsCheck();

            Span<Move> childPV = stackalloc Move[MAX_PLY];
            Value bestValue = Value.Min;
            Move bestMove = Move.None;
            int moveCount = 0;

            if (pvNode)
                pv[0] = Move.None;              

            if (ply >= MAX_PLY)
                return (ply >= MAX_PLY && !ss[ply].inCheck) ? Evaluation.Evaluate(rootPosition)
                                                            : Value.Draw;

            //Mate distance pruning
            alpha = Max(MatedIn(ply), alpha);
            beta = Min(MateIn(ply + 1), beta);
            if (alpha >= beta)
                return alpha;

            Depth ttDepth = ss[ply].inCheck || depth >= Depth.QSearch_Checks ? Depth.QSearch_Checks
                                                                 : Depth.QSearch_NoChecks;

            //Transposition table lookup
            Key positionKey = rootPosition.ZobristKey;
            ref TranspositionTable.Entry ttentry = ref Engine.TTTable.ProbeTT(positionKey, out ss[ply].ttHit);
            Value ttValue = ss[ply].ttHit ? ttentry.Evaluation : Value.Zero;
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
                bestValue = Value.Min;
            }
            else
            {
                if (ss[ply].ttHit)
                {
                    if ((ss[ply].staticEval = bestValue = ttentry.Evaluation) == 0)
                        ss[ply].staticEval = bestValue = Evaluation.Evaluate(rootPosition);

                    if (ttValue != 0
                        && (ttentry.BoundType & (ttValue > bestValue ? Bound.Lower : Bound.Upper)) != 0)
                        bestValue = ttValue;
                }
                else
                    ss[ply].staticEval = bestValue = Evaluation.Evaluate(rootPosition);

                //Stand pat
                if (bestValue >= beta)
                {
                    if (!ss[ply].ttHit)
                        ttentry.Save(positionKey, pvNode, Move.None, Depth.None, Bound.Lower, ss[ply].staticEval);

                    return bestValue;
                }

                if (pvNode && bestValue > alpha)
                    alpha = bestValue;
            }
            
            MovePicker mp = new(rootPosition, ttMove, Square.a1, stackalloc MoveScore[MAX_MOVE_COUNT]);
            for (Move move; ((move = mp.NextMove()) != Move.None);)
            {
                if (!rootPosition.IsLegal(move))
                    continue;

                moveCount++;
                bool givesCheck = rootPosition.GivesCheck(move);

                //If we are not in a desperate situation we can skip the moves that returns a negative Static Exchange Evaluation
                if (bestValue > Value.KnownLoss
                    && !rootPosition.SeeGe(move))
                    continue;

                rootPosition.MakeMove(move, states[ply], givesCheck);

                Value value = QSearch(nodeType, ss, beta.Negate(), alpha.Negate(), ply + 1, childPV, depth - 1).Negate();

                rootPosition.Takeback(move);

                if (stop)
                    return 0;

                Debug.Assert(value > Value.Min && value < Value.Max);

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
            if (ss[ply].inCheck && bestValue == Value.Min)
            {
                Debug.Assert(new MoveList(rootPosition, stackalloc MoveScore[MAX_MOVE_COUNT]).Count == 0);
                return MatedIn(ply);
            }

            ttentry.Save(positionKey, pvNode, bestMove, ttDepth,
                bestValue >= beta ? Bound.Lower
                                 : pvNode && bestMove != Move.None ? Bound.Exact
                                                                    : Bound.Upper, bestValue);

            return bestValue;
        }

        public class RootMoves
        {
            readonly RootMove[] buffer = new RootMove[MAX_MOVE_COUNT];
            public int Count { get; private set; }

            public bool Contains(Move m)
            {
                for (int i = 0; i < Count; i++)
                    if (buffer[i].Move == m)
                        return true;
                return false;
            }

            public RootMove? Find(Move m)
            {
                for (int i = 0; i < Count; i++)
                    if (buffer[i].Move == m)
                        return buffer[i];
                return null;
            }

            public void Sort(int start = 0)
            {
                Utility.PartialInsertionSort(buffer, start, Count);
            }

            public void Add(RootMove rootMove)
            {
                buffer[Count++] = rootMove;
            }

            public void Clear()
            {
                Count = 0;
            }

            public Enumerator GetEnumerator()
            {
                return new(this);
            }
            public struct Enumerator
            {
                readonly RootMoves rm;
                private int idx;

                public RootMove Current { get; private set; }

                public Enumerator(RootMoves rms)
                {
                    rm = rms;
                    idx = -1;

                    Current = rm.buffer[0];
                }

                public bool MoveNext()
                {
                    if (++idx < rm.Count)
                    {
                        Current = rm.buffer[++idx];
                        return true;
                    }
                    return false;
                }
            }

            public RootMove this[int i]
            {
                get => buffer[i];
            }
        }

        public class RootMove : IArithmeticComparable<RootMove>
        {
            public readonly Move[] pv = new Move[MAX_PLY];
            public Value score;
            public Value previousScore;
            public Value uciScore;
            public bool boundUpper;
            public bool boundLower;
            public Depth selDepth;

            public RootMove()
            {

            }
            public RootMove(Move m)
            {
                pv[0] = m;
            }

            public Move Move
            {
                get => pv[0];
                set
                {
                    Array.Clear(pv);
                    pv[0] = value;
                }
            }

            public int CompareTo(RootMove? other)
            {
                if (other is null)
                    return 1;

                return score != other.score ? score.CompareTo(other.score)
                                            : previousScore.CompareTo(other.previousScore);
            }

            public static bool operator <(RootMove lhs, RootMove rhs)
            {
                return lhs.score != rhs.score ? lhs.score < rhs.score
                                                : lhs.previousScore < rhs.previousScore;
            }
            public static bool operator >(RootMove lhs, RootMove rhs)
            {
                return lhs.score != rhs.score ? lhs.score > rhs.score
                                                : lhs.previousScore > rhs.previousScore;
            }
        }

        public struct SearchStats
        {
            public Depth completedDepth;
            public Depth selectiveDepth;
            public ulong nodeCount;
            public ulong qNodeCount;
        }

        public struct SearchStack
        {
            public bool inCheck;
            public bool ttHit;
            public Value staticEval;
        }
    }
}
