using static Chessour.Bitboards;
using static Chessour.GenerationType;

namespace Chessour
{
    enum GenerationType
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

    static class MoveGenerator
    {
        public const int MaxMoveCount = 218;

        public static int Generate(Position position, Span<MoveScore> buffer, int start = 0)
        {
            Color us = position.ActiveColor;
            Bitboard pinned = position.BlockersForKing(us) & position.Pieces(us);
            Square ksq = position.KingSquare(us);

            int end = position.IsCheck() ? Generate(Evasions, position, buffer, start)
                                    : Generate(NonEvasions, position, buffer, start);

            for (; start < end; start++)
            {
                Move m = buffer[start].Move;

                if (((pinned != 0 && (pinned & m.FromSquare().ToBitboard()) != 0) || m.FromSquare() == ksq || m.TypeOf() == MoveType.EnPassant)
                    && !position.IsLegal(m))
                    buffer[start--] = buffer[--end].Move;
                else
                    continue;
            }

            return end;
        }

        public static int Generate(GenerationType type, Position position, Span<MoveScore> buffer, int start = 0)
        {
            Color us = position.ActiveColor;
            Square ksq = position.KingSquare(us);
            Bitboard occupancy = position.Pieces();

            Bitboard targetSquares = type == Evasions ? Between(ksq, position.Checkers.LeastSignificantSquare()) | position.Checkers
                               : type == NonEvasions ? ~position.Pieces(us)
                               : type == Captures ? position.Pieces(us.Flip())
                               : ~position.Pieces();
            if (type != Evasions || !position.Checkers.MoreThanOne())
            {
                start = us == Color.White ? GenerateWhitePawnMoves(type, position, targetSquares, buffer, start)
                                          : GenerateBlackPawnMoves(type, position, targetSquares, buffer, start);
                start = GeneratePieceMoves(PieceType.Knight, us, position, targetSquares, occupancy, buffer, start);
                start = GeneratePieceMoves(PieceType.Bishop, us, position, targetSquares, occupancy, buffer, start);
                start = GeneratePieceMoves(PieceType.Rook, us, position, targetSquares, occupancy, buffer, start);
                start = GeneratePieceMoves(PieceType.Queen, us, position, targetSquares, occupancy, buffer, start);
            }

            //King moves
            Bitboard kingAttacks = Attacks(PieceType.King, ksq) & (type == Evasions ? ~position.Pieces(us) : targetSquares);

            foreach (Square attack in kingAttacks)
                buffer[start++] = MakeMove(ksq, attack);

            if ((type == Quiets || type == NonEvasions) && position.CanCastle(MakeCastlingRight(us, CastlingRight.All)))
            {
                CastlingRight kingSide = MakeCastlingRight(us, CastlingRight.KingSide);
                if (position.CanCastle(kingSide) && !position.CastlingImpeded(kingSide))
                    buffer[start++] = MakeMove(ksq, position.CastlingRookSquare(kingSide), MoveType.Castling);

                CastlingRight queenSide = MakeCastlingRight(us, CastlingRight.QueenSide);
                if (position.CanCastle(queenSide) && !position.CastlingImpeded(queenSide))
                    buffer[start++] = MakeMove(ksq, position.CastlingRookSquare(queenSide), MoveType.Castling);
            }

            return start;
        }

        private static int GeneratePromotions(GenerationType type, Square from, Square to, Span<MoveScore> buffer, int start)
        {
            if (type == Captures || type == Evasions || type == NonEvasions)
                buffer[start++] = MakeMove(from, to, MoveType.Promotion, PieceType.Queen);

            if (type == Quiets || type == Evasions || type == NonEvasions)
            {
                buffer[start++] = MakeMove(from, to, MoveType.Promotion, PieceType.Rook);
                buffer[start++] = MakeMove(from, to, MoveType.Promotion, PieceType.Bishop);
                buffer[start++] = MakeMove(from, to, MoveType.Promotion, PieceType.Knight);
            }

            return start;
        }

        private static int GenerateWhitePawnMoves(GenerationType type, Position position, Bitboard targets, Span<MoveScore> buffer, int start)
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
                    buffer[start++] = MakeMove(to.NegativeShift(Up), to);

                foreach (Square to in push2)
                    buffer[start++] = MakeMove(to.NegativeShift(Up).NegativeShift(Up), to);
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
                    start = GeneratePromotions(type, to.NegativeShift(Up), to, buffer, start);

                foreach (Square to in captureRight)
                    start = GeneratePromotions(type, to.NegativeShift(UpRight), to, buffer, start);

                foreach (Square to in captureLeft)
                    start = GeneratePromotions(type, to.NegativeShift(UpLeft), to, buffer, start);
            }


            //Captures
            if (type == Captures || type == Evasions || type == NonEvasions)
            {
                Bitboard captureRight = pawns.ShiftNorthEast() & enemies;
                Bitboard captureLeft = pawns.ShiftNorthWest() & enemies;

                foreach (Square to in captureRight)
                    buffer[start++] = MakeMove(to.NegativeShift(UpRight), to);

                foreach (Square to in captureLeft)
                    buffer[start++] = MakeMove(to.NegativeShift(UpLeft), to);

                if (position.EnPassantSquare != Square.None)
                {
                    if (type == Evasions && (targets & (position.EnPassantSquare.Shift(Up).ToBitboard())) != 0)
                        return start;

                    Bitboard epCandidates = pawns & PawnAttacks(enemy, position.EnPassantSquare);

                    foreach (Square from in epCandidates)
                        buffer[start++] = MakeMove(from, position.EnPassantSquare, MoveType.EnPassant);
                }
            }
            return start;
        }
      
        private static int GenerateBlackPawnMoves(GenerationType type, Position position, Bitboard targets, Span<MoveScore> buffer, int start)
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
                    buffer[start++] = MakeMove(to.NegativeShift(Up), to);

                foreach (Square to in push2)
                    buffer[start++] = MakeMove(to.NegativeShift(Up).NegativeShift(Up), to);
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
                    start = GeneratePromotions(type, to.NegativeShift(Up), to, buffer, start);

                foreach (Square to in captureRight)
                    start = GeneratePromotions(type, to.NegativeShift(UpRight), to, buffer, start);

                foreach (Square to in captureLeft)
                    start = GeneratePromotions(type, to.NegativeShift(UpLeft), to, buffer, start);
            }


            //Captures
            if (type == Captures || type == Evasions || type == NonEvasions)
            {
                Bitboard captureRight = pawns.ShiftSouthWest() & enemies;
                Bitboard captureLeft = pawns.ShiftSouthEast() & enemies;

                foreach (Square to in captureRight)
                    buffer[start++] = MakeMove(to.NegativeShift(UpRight), to);

                foreach (Square to in captureLeft)
                    buffer[start++] = MakeMove(to.NegativeShift(UpLeft), to);

                if (position.EnPassantSquare != Square.None)
                {
                    if (type == Evasions && (targets & (position.EnPassantSquare.Shift(Up).ToBitboard())) != 0)
                        return start;

                    Bitboard epCandidates = pawns & PawnAttacks(enemy, position.EnPassantSquare);

                    foreach (Square from in epCandidates)
                        buffer[start++] = MakeMove(from, position.EnPassantSquare, MoveType.EnPassant);
                }
            }
            return start;
        }

        private static int GeneratePieceMoves(PieceType pieceType, Color us, Position position, Bitboard targetSquares, Bitboard occupiedSquares, Span<MoveScore> buffer, int start)
        {
            foreach (Square pieceSquare in position.Pieces(us, pieceType))
                foreach (Square attackSquare in Attacks(pieceType, pieceSquare, occupiedSquares) & targetSquares)
                    buffer[start++] = MakeMove(pieceSquare, attackSquare);

            return start;
        }
    }
}
