using Chessour.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static Chessour.SearchContext;
using static Chessour.Types.ValueUtills;

namespace Chessour
{
    public enum NodeType { NonPV, PV, Root }
    class RootMoves : List<RootMove>
    {
        public RootMoves() : base(MoveList.MaxMoveCount) { }

        public void Add(Move m) => Add(new RootMove(m));
        public bool Contains(Move m)
        {
            foreach (RootMove rm in this)
                if (rm.Move == m)
                    return true;
            return false;
        }
        public RootMove Find(Move m)
        {
            foreach (var rm in this)
                if (rm.Move == m)
                    return rm;

            throw new InvalidOperationException();
        }

        public new void Sort()
        {
            int n = Count;
            for (int i = 1; i < n; ++i)
            {
                RootMove rm = this[i];
                int j = i - 1;

                // Move elements of arr[0..i-1],
                // that are greater than key,
                // to one position ahead of
                // their current position
                while (j >= 0 && this[j] < rm)
                {
                    this[j + 1] = this[j];
                    j--;
                }
                this[j + 1] = rm;
            }
        }
    }
    class RootMove
    {
        public Move[] pv = new Move[MaxDepth];
        public Value Score { get; set; }
        public Value PreviousScore { get; set; }
        public Value UCIScore { get; set; }
        public bool LowerBound { get; set; }
        public bool UpperBound { get; set; }

        public Move Move { get => pv[0]; }
        public Move Refutation { get => pv[1]; }
        public RootMove(Move m)
        {
            pv[0] = m;
        }

        public static bool operator <(RootMove lhs, RootMove rhs)
        {
            return lhs.Score != rhs.Score ? lhs.Score < rhs.Score
                                            : lhs.PreviousScore < rhs.PreviousScore;
        }
        public static bool operator >(RootMove lhs, RootMove rhs) => rhs < lhs;
    }
    class SearchStack
    {
        public Move[] pv = new Move[MaxDepth];
        public bool inCheck;
        public bool ttHit;
        public bool ttPv;
        public Value staticEval;
    }

    class SearchContext
    {
        public const int MaxDepth = 128;
        public const int MaxPly = 128;
        public struct SearchLimits
        {
            public List<Move> SearchMoves { get; }
            public int Perft { get; set; }
            public int Depth { get; set; }

            public SearchLimits()
            {
                SearchMoves = new();
                Perft = Depth = 0;
            }
        }

        public bool Stop { get; set; }

        public RootMoves rootMoves;
        public readonly SearchStack[] searchStack;
        readonly FastStack<Position.StateInfo> stateInfos;
        public SearchLimits limits;
        public ulong NodeCount { get; private set; }
        public ulong QNodeCount { get; private set; }

        public SearchContext()
        {
            stateInfos = new(MaxPly);
            searchStack = new SearchStack[MaxPly];
            rootMoves = new();

            for (int i = 0; i < MaxPly; i++)
            {
                stateInfos.Push(new Position.StateInfo());
                searchStack[i] = new();
            }
        }
        public ulong Perft(Position position, int depth, bool divide = true)
        {
            static bool IsLeaf(int depth) => depth == 2;

            ulong branchNodes, nodes = 0;
            Position.StateInfo state = stateInfos.Pop();

            MoveList moves = new(position, stackalloc MoveScore[MoveList.MaxMoveCount]);

            foreach (Move m in moves)
            {
                if (divide && depth <= 1)
                    nodes += branchNodes = 1;

                else
                {
                    position.MakeMove(m, state);

                    branchNodes = IsLeaf(depth) ? (ulong)new MoveList(position, stackalloc MoveScore[MoveList.MaxMoveCount]).Count
                                                : Perft(position, depth - 1, false);

                    nodes += branchNodes;
                    position.Takeback();
                }
                if (divide)
                    Console.WriteLine($"{UCI.ToString(m)}: {branchNodes}");
            }

            stateInfos.Push(state);
            return nodes;
        }

        public void Reset()
        {
            NodeCount = 0;
            QNodeCount = 0;
        }

        public enum NodeTypes
        {
            Root,
            PvNode,
            NonPvNode
        }

        public Value Search(NodeType nodeType, Position position, int ply, Value alpha, Value beta, int depth)
        {
            bool root = nodeType == NodeType.Root;
            bool pvNode = nodeType != NodeType.NonPV;

            if (depth <= 0)
                return QSearch(nodeType, position, ply, alpha, beta);

            if (!root)
            {

                //Mate distance pruning
                alpha = (Value)Math.Max((int)MatedIn(ply), (int)alpha);
                beta = (Value)Math.Min((int)MateIn(ply + 1), (int)beta);
                if (alpha >= beta)
                    return alpha;               
            }

            int moveCount;
            Value bestValue, ttValue, value;
            Move bestMove, ttMove, move;
            int baseDepth = depth;

            Color us = position.ActiveColor;
            NodeCount++;
            searchStack[ply].inCheck = position.IsCheck();
            searchStack[ply].ttPv = false;

            bestValue = value = Value.Min;
            bestMove = Move.None;
            moveCount = 0;


            Key posKey = position.ZobristKey;

            ref TTEntry ttEntry = ref TranspositionTable.ProbeTT(posKey, out searchStack[ply].ttHit);
            ttValue = searchStack[ply].ttHit ? ttEntry.Evaluation : 0;
            ttMove = root ? rootMoves[0].Move
                          : searchStack[ply].ttHit ? ttEntry.Move
                                                   : Move.None;

            //Moves loop
            MovePicker mp = new(position, ttMove, stackalloc MoveScore[MoveList.MaxMoveCount]);

            Position.StateInfo st = stateInfos.Pop();
            while ((move = mp.NextMove()) != Move.None)
            {
                if (!position.IsLegal(move))
                    continue;


                //Count the number of legal moves
                moveCount++;
                bool givesCheck = position.GivesCheck(move);

                //If we return withouth doing any moves in a non pv move that causes a beta cutoff we would get the wrong line
                //This basicaly chops the entire pv line as it should already be recorded in this ply's searchstack
                if (pvNode)
                    searchStack[ply + 1].pv[0] = Move.None;

                position.MakeMove(move, st, givesCheck);

                //For any node outside of the pv we are doing aspiration searches to prove our pv line is just fine
                //For more information read PVS and Aspiration section in : https://www.chessprogramming.org/Principal_Variation_Search
                if (!pvNode || moveCount > 1)
                    value = Search(NodeType.NonPV, position, ply + 1, (alpha + 1).Negate(), alpha.Negate(), depth - 1).Negate();

                if (pvNode //If aspiration search fails it will fail back to one of the pv nodes and from there we will start a pvNode search
                    && (moveCount == 1 // It is the first move we search on this node so therefor part of the pv
                       || (value > alpha // Value is higher than alpha 
                        && (root || value < beta)))) //But lower than beta except when we are on the root node
                                                     //They cant prevent us from doing the move if we are at the root node
                {
                    searchStack[ply + 1].pv[0] = Move.None;

                    value = Search(NodeType.PV, position, ply + 1, beta.Negate(), alpha.Negate(), depth - 1).Negate();
                }

                position.Takeback();

                Debug.Assert(value > Value.Min && value < Value.Max);

                if (Stop)
                    break;

                if (root)
                {
                    //Update the rootMoves list
                    RootMove rm = rootMoves.Find(move);

                    if (moveCount == 1 || value > alpha)
                    {
                        rm.Score = value;
                        rm.LowerBound = rm.UpperBound = false;

                        if (value >= beta)
                        {
                            rm.LowerBound = true;
                            rm.UCIScore = beta;
                        }
                        else if (value <= alpha)
                        {
                            rm.UpperBound = true;
                            rm.UCIScore = alpha;
                        }

                        Array.Clear(rm.pv);
                        rm.pv[0] = move;

                        UpdatePV(rm.pv, move, searchStack[ply + 1].pv);
                    }
                    else
                        rm.Score = Value.Min;
                }

                if (value > bestValue)
                {
                    bestValue = value;

                    if (value > alpha)
                    {
                        bestMove = move;

                        if (pvNode && !root)
                        {
                            UpdatePV(searchStack[ply].pv, move, searchStack[ply + 1].pv);
                        }

                        if (pvNode && value < beta)
                        {
                            alpha = value;
                            /*
                            //We are happy with one improvement so we can shorten searches of the other moves to speed up overal search                            
                            if (depth > 1
                            && depth < 6
                            && beta < Value.KnownWin
                            && alpha > Value.KnownLoss)
                                depth -= 1;
                            */
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
            //Return the state object
            stateInfos.Push(st);

            if (Stop)
            {
                return 0;
            }


            if (moveCount == 0)
            {
                bestValue = searchStack[ply].inCheck ? MatedIn(ply)
                                                    : Value.Draw;
            }

            ttEntry.Save(posKey, depth, move, pvNode,
                bestValue >= beta ? Bound.LowerBound
                                  : pvNode && bestMove != Move.None ? Bound.Exact
                                                                    : Bound.UpperBound, bestValue);

            return bestValue;
        }

        private Value QSearch(NodeType nodeType, Position position, int ply, Value alpha, Value beta, int depth = 0)
        {
            bool root = nodeType == NodeType.Root;
            bool pvNode = nodeType != NodeType.NonPV;

            Debug.Assert(nodeType != NodeType.Root); // We shouldn be entering qSearch at root node
            Debug.Assert(alpha >= Value.Min && alpha < beta && beta <= Value.Max); //Value is not outside the defined bounds
            Debug.Assert(pvNode || (alpha == beta - 1)); //This is a pv node or we are in a aspiration search
            Debug.Assert(depth <= 0); //Depth is not outside defined bounds

            Key posKey;
            int moveCount;
            Value bestValue, ttValue, value;
            Move bestMove, ttMove, move;

            QNodeCount++;
            searchStack[ply].inCheck = position.IsCheck();
            moveCount = 0;
            bestValue = value = Value.Min;
            bestMove = Move.None;


            //If we return from this node because of some pruning without doing any moves
            //we need to prevent the parent from getting the wrong pv line as pv arrays are shared between children
            if (pvNode)
                searchStack[ply].pv[0] = Move.None;

            //As qSearch is not bound for any depth limitations check if we reached max plyCount
            if (ply >= MaxPly)
                return (ply >= MaxPly && !searchStack[ply].inCheck) ? Evaluation.Evaluate(position) : 0;

            //Transposition table
            posKey = position.ZobristKey;
            ref var ttEntry = ref TranspositionTable.ProbeTT(posKey, out searchStack[ply].ttHit);
            ttValue = searchStack[ply].ttHit ? ttEntry.Evaluation : 0;
            ttMove = searchStack[ply].ttHit ? ttEntry.Move : Move.None;

            //Static evaluation
            //We will use this evaluation to perform the stand pat
            //We just need to be sure that this position is stable enough to evaluate properly
            if (searchStack[ply].inCheck)
            {
                searchStack[ply].staticEval = 0;
                bestValue = Value.Min;
            }
            else
            {
                searchStack[ply].staticEval = bestValue = Evaluation.Evaluate(position);
            }

            //Stand pat
            if (bestValue >= beta)
            {
                return bestValue;
            }

            if (pvNode && bestValue > alpha)
                alpha = bestValue;


            //Moves loop
            MovePicker mp = new(position, Move.None, Square.a1, stackalloc MoveScore[MoveList.MaxMoveCount]);

            Position.StateInfo st = stateInfos.Pop();
            while ((move = mp.NextMove()) != Move.None)
            {
                if (!position.IsLegal(move))
                    continue;

                moveCount++;
                bool givesCheck = position.GivesCheck(move);

                //If we are not in a desperate situation we can skip the moves that returns a negative SEE
                if (bestValue > Value.KnownLoss
                    && !position.SeeGe(move))
                    continue;

                position.MakeMove(move, st, givesCheck);
                value = QSearch(nodeType, position, ply + 1, beta.Negate(), alpha.Negate(), depth - 1).Negate();
                position.Takeback();

                if (Stop)
                    break;

                Debug.Assert(value > Value.Min && value < Value.Max);


                if (value > bestValue)
                {
                    bestValue = value;

                    if (value > alpha)
                    {
                        bestMove = move;

                        if (pvNode)
                            UpdatePV(searchStack[ply].pv, move, searchStack[ply + 1].pv);

                        if (pvNode && value < beta)
                            alpha = value;

                        else
                            break;   //Fail high                    
                    }
                }
            }
            stateInfos.Push(st);

            if (Stop)
                return 0;

            //After searching every move if we have found no legal moves and we are in check we are mated
            if (searchStack[ply].inCheck && bestValue == Value.Min)
            {
                Debug.Assert(new MoveList(position, stackalloc MoveScore[MoveList.MaxMoveCount]).Count == 0);
                return MatedIn(ply);
            }

            ttEntry.Save(posKey, depth, bestMove, pvNode,
                bestValue >= beta ? Bound.LowerBound
                                  : Bound.UpperBound,
                searchStack[ply].staticEval);

            Debug.Assert(bestValue > Value.Min && bestValue < Value.Max);
            return bestValue;
        }
        private static void UpdatePV(Move[] pv, Move move, Move[] childPv)
        {
            int i = 0, j = 0;
            pv[i++] = move;
            while (childPv[j] != Move.None) // Has the child pv ended
                pv[i++] = childPv[j++]; // Copy the moves from the child pv
            pv[i] = Move.None; // Put this at the end to represent pv line ended
        }
    }
}
