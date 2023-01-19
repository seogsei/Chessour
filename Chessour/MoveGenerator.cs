using static Chessour.Bitboards;
using static Chessour.GenerationType;

namespace Chessour
{
    public enum GenerationType
    {
        Captures,
        Quiets,
        NonEvasions,
        Evasions,
    }
    ref struct MoveList
    {
        readonly Span<MoveScore> moves;
        public int Count { get; private set; }

        public MoveList(Span<MoveScore> buffer)
        {
            Debug.Assert(buffer.Length == MoveGenerator.MaxMoveCount);

            moves = buffer;
            Count = 0;
        }

        public MoveList(Position position, Span<MoveScore> buffer) : this(buffer)
        {
            Generate(position);
        }
        public MoveList(GenerationType type, Position position, Span<MoveScore> buffer) : this(buffer)
        {
            Generate(type, position);
        }

        public void Generate(Position position)
        {
            Count = MoveGenerator.Generate(position, moves, Count);
        }
        public void Generate(GenerationType type, Position position)
        {
            Count = MoveGenerator.Generate(type, position, moves, Count);
        }

        public bool Contains(Move m)
        {
            foreach (Move move in this)
                if (move == m)
                    return true;
            return false;
        }

        public Enumerator GetEnumerator()
        {
            return new(this);
        }
        public ref struct Enumerator
        {
            MoveList moveList;
            int idx;

            public MoveScore Current => moveList.moves[idx];

            public Enumerator(MoveList moveList)
            {
                this.moveList = moveList;
                idx = -1;
            }

            public bool MoveNext()
            {
                return ++idx < moveList.Count;
            }
        }
    }

    public static class MoveGenerator
    {
        public const int MaxMoveCount = 218;

        public static int Generate(Position position, Span<MoveScore> moveList, int start = 0)
        {
            Color us = position.ActiveColor;
            Bitboard pinned = position.BlockersForKing(us) & position.Pieces(us);
            Square ksq = position.KingSquare(us);

            int end = position.IsCheck() ? Generate(Evasions, position, moveList, start)
                                    : Generate(NonEvasions, position, moveList, start);

            for (; start < end; start++)
            {
                Move m = moveList[start].Move;

                if (((pinned != 0 && (pinned & m.FromSquare().ToBitboard()) != 0) || m.FromSquare() == ksq || m.TypeOf() == MoveType.EnPassant)
                    && !position.IsLegal(m))
                    moveList[start--] = moveList[--end].Move;
                else
                    continue;
            }

            return end;
        }

        public static int Generate(GenerationType type, Position position, Span<MoveScore> moveList, int start = 0)
        {
            Color us = position.ActiveColor;
            Square ksq = position.KingSquare(us);
            Bitboard occupancy = position.Pieces();

            Bitboard targetSquares = type == Evasions ? Between(ksq, position.Checkers.LeastSignificantSquare()) | position.Checkers
                               : type == NonEvasions ? ~position.Pieces(us)
                               : type == Captures ? position.Pieces(us.Opposite())
                               : ~position.Pieces();
            if (type != Evasions || !position.Checkers.MoreThanOne())
            {
                start = us == Color.White ? GenerateWhitePawnMoves(type, position, targetSquares, moveList, start)
                                          : GenerateBlackPawnMoves(type, position, targetSquares, moveList, start);
                start = GenerateKnightMoves(us, position, targetSquares, moveList, start);
                start = GeneratePieceMoves(PieceType.Bishop, us, position, targetSquares, occupancy, moveList, start);
                start = GeneratePieceMoves(PieceType.Rook, us, position, targetSquares, occupancy, moveList, start);
                start = GeneratePieceMoves(PieceType.Queen, us, position, targetSquares, occupancy, moveList, start);
            }

            //King moves
            Bitboard kingAttacks = Attacks(PieceType.King, ksq) & (type == Evasions ? ~position.Pieces(us) : targetSquares);

            foreach (Square attack in kingAttacks)
                moveList[start++] = MakeMove(ksq, attack);

            if ((type == Quiets || type == NonEvasions) && position.CanCastle(MakeCastlingRight(us, CastlingRight.All)))
            {
                CastlingRight kingSide = MakeCastlingRight(us, CastlingRight.KingSide);
                if (position.CanCastle(kingSide) && !position.CastlingImpeded(kingSide))
                    moveList[start++] = MakeCastlingMove(ksq, position.CastlingRookSquare(kingSide));

                CastlingRight queenSide = MakeCastlingRight(us, CastlingRight.QueenSide);
                if (position.CanCastle(queenSide) && !position.CastlingImpeded(queenSide))
                    moveList[start++] = MakeCastlingMove(ksq, position.CastlingRookSquare(queenSide));
            }

            return start;
        }

        private static int GeneratePromotions(GenerationType type, Square from, Square to, Span<MoveScore> moveList, int start)
        {
            if (type == Captures || type == Evasions || type == NonEvasions)
                moveList[start++] = MakePromotionMove(from, to, PieceType.Queen);

            if (type == Quiets || type == Evasions || type == NonEvasions)
            {
                moveList[start++] = MakePromotionMove(from, to, PieceType.Rook);
                moveList[start++] = MakePromotionMove(from, to, PieceType.Bishop);
                moveList[start++] = MakePromotionMove(from, to, PieceType.Knight);
            }

            return start;
        }

        private static int GenerateWhitePawnMoves(GenerationType type, Position position, Bitboard targets, Span<MoveScore> moveList, int start)
        {
            const Color us = Color.White;
            const Color enemy = Color.Black;

            const Direction Up = us == Color.White ? Direction.North : Direction.South;
            const Direction UpRight = us == Color.White ? Direction.NorthEast : Direction.SouthWest;
            const Direction UpLeft = us == Color.White ? Direction.NorthWest : Direction.SouthEast;

            const Bitboard RelativeRank7 = us == Color.White ? Bitboard.Rank7 : Bitboard.Rank2;
            const Bitboard RelativeRank3 = us == Color.White ? Bitboard.Rank3 : Bitboard.Rank6;

            Bitboard emptySquares = ~position.Pieces();
            Bitboard enemies = type == Evasions ? position.Checkers
                                                          : position.Pieces(enemy);

            Bitboard pawns = position.Pieces(us, PieceType.Pawn) & ~RelativeRank7;
            Bitboard promotionPawns = position.Pieces(us, PieceType.Pawn) & RelativeRank7;

            //Pushes except promotions
            if (type != Captures)
            {
                Bitboard push1 = pawns.ShiftNorth() & emptySquares;
                Bitboard push2 = (push1 & RelativeRank3).ShiftNorth() & emptySquares;

                if (type == Evasions)
                {
                    push1 &= targets;
                    push2 &= targets;
                }

                foreach (Square to in push1)
                    moveList[start++] = MakeMove(to.NegativeShift(Up), to);

                foreach (Square to in push2)
                    moveList[start++] = MakeMove(to.NegativeShift(Up).NegativeShift(Up), to);
            }

            //Promotions
            if (promotionPawns != 0)
            {
                Bitboard push = promotionPawns.ShiftNorth() & emptySquares;
                if (type == Evasions)
                    push &= targets;

                Bitboard captureRight = promotionPawns.ShiftNorthEast() & enemies;
                Bitboard captureLeft = promotionPawns.ShiftNorthWest() & enemies;

                foreach (Square to in push)
                    start = GeneratePromotions(type, to.NegativeShift(Up), to, moveList, start);

                foreach (Square to in captureRight)
                    start = GeneratePromotions(type, to.NegativeShift(UpRight), to, moveList, start);

                foreach (Square to in captureLeft)
                    start = GeneratePromotions(type, to.NegativeShift(UpLeft), to, moveList, start);
            }


            //Captures
            if (type == Captures || type == Evasions || type == NonEvasions)
            {
                Bitboard captureRight = pawns.ShiftNorthEast() & enemies;
                Bitboard captureLeft = pawns.ShiftNorthWest() & enemies;

                foreach (Square to in captureRight)
                    moveList[start++] = MakeMove(to.NegativeShift(UpRight), to);

                foreach (Square to in captureLeft)
                    moveList[start++] = MakeMove(to.NegativeShift(UpLeft), to);

                if (position.EnPassantSquare != Square.None)
                {
                    if (type == Evasions && (targets & (position.EnPassantSquare.Shift(Up).ToBitboard())) != 0)
                        return start;

                    Bitboard epCandidates = pawns & PawnAttacks(enemy, position.EnPassantSquare);

                    foreach (Square from in epCandidates)
                        moveList[start++] = MakeEnPassantMove(from, position.EnPassantSquare);
                }
            }
            return start;
        }

        private static int GenerateBlackPawnMoves(GenerationType type, Position position, Bitboard targets, Span<MoveScore> moveList, int start)
        {
            const Color us = Color.Black;
            const Color enemy = Color.White;

            const Direction Up = us == Color.White ? Direction.North : Direction.South;
            const Direction UpRight = us == Color.White ? Direction.NorthEast : Direction.SouthWest;
            const Direction UpLeft = us == Color.White ? Direction.NorthWest : Direction.SouthEast;

            const Bitboard RelativeRank7 = us == Color.White ? Bitboard.Rank7 : Bitboard.Rank2;
            const Bitboard RelativeRank3 = us == Color.White ? Bitboard.Rank3 : Bitboard.Rank6;

            Bitboard emptySquares = ~position.Pieces();
            Bitboard enemies = type == Evasions ? position.Checkers
                                                          : position.Pieces(enemy);

            Bitboard pawns = position.Pieces(us, PieceType.Pawn) & ~RelativeRank7;
            Bitboard promotionPawns = position.Pieces(us, PieceType.Pawn) & RelativeRank7;

            //Pushes except promotions
            if (type != Captures)
            {
                Bitboard push1 = pawns.ShiftSouth() & emptySquares;
                Bitboard push2 = (push1 & RelativeRank3).ShiftSouth() & emptySquares;

                if (type == Evasions)
                {
                    push1 &= targets;
                    push2 &= targets;
                }

                foreach (Square to in push1)
                    moveList[start++] = MakeMove(to.NegativeShift(Up), to);

                foreach (Square to in push2)
                    moveList[start++] = MakeMove(to.NegativeShift(Up).NegativeShift(Up), to);
            }

            //Promotions
            if (promotionPawns != 0)
            {
                Bitboard push = promotionPawns.ShiftSouth() & emptySquares;
                if (type == Evasions)
                    push &= targets;

                Bitboard captureRight = promotionPawns.ShiftSouthWest() & enemies;
                Bitboard captureLeft = promotionPawns.ShiftSouthEast() & enemies;

                foreach (Square to in push)
                    start = GeneratePromotions(type, to.NegativeShift(Up), to, moveList, start);

                foreach (Square to in captureRight)
                    start = GeneratePromotions(type, to.NegativeShift(UpRight), to, moveList, start);

                foreach (Square to in captureLeft)
                    start = GeneratePromotions(type, to.NegativeShift(UpLeft), to, moveList, start);
            }


            //Captures
            if (type == Captures || type == Evasions || type == NonEvasions)
            {
                Bitboard captureRight = pawns.ShiftSouthWest() & enemies;
                Bitboard captureLeft = pawns.ShiftSouthEast() & enemies;

                foreach (Square to in captureRight)
                    moveList[start++] = MakeMove(to.NegativeShift(UpRight), to);

                foreach (Square to in captureLeft)
                    moveList[start++] = MakeMove(to.NegativeShift(UpLeft), to);

                if (position.EnPassantSquare != Square.None)
                {
                    if (type == Evasions && (targets & (position.EnPassantSquare.Shift(Up).ToBitboard())) != 0)
                        return start;

                    Bitboard epCandidates = pawns & PawnAttacks(enemy, position.EnPassantSquare);

                    foreach (Square from in epCandidates)
                        moveList[start++] = MakeEnPassantMove(from, position.EnPassantSquare);
                }
            }
            return start;
        }

        private static int GenerateKnightMoves(Color us, Position position, Bitboard targetSquares, Span<MoveScore> moveList, int start)
        {
            foreach (Square knightSqr in position.Pieces(us, PieceType.Knight))
                foreach (Square attack in Attacks(PieceType.Knight, knightSqr) & targetSquares)
                    moveList[start++] = MakeMove(knightSqr, attack);

            return start;
        }

        private static int GeneratePieceMoves(PieceType pt, Color us, Position position, Bitboard targetSquares, Bitboard occupiedSquares, Span<MoveScore> moveList, int start)
        {
            foreach (Square pieces in position.Pieces(us, pt))
                foreach (Square attack in Attacks(pt, pieces, occupiedSquares) & targetSquares)
                    moveList[start++] = MakeMove(pieces, attack);

            return start;
        }
    }
}
