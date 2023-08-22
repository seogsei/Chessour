using Chessour.Search;
using static Chessour.Bitboards;
using static Chessour.GenerationType;
using static Chessour.MoveExtensions;

namespace Chessour
{
    public enum GenerationType
    {
        Captures,
        Quiets,
        NonEvasions,
        Evasions,
    }

    public static class MoveGenerator
    {
        public const int MAX_MOVE_COUNT = 256;
        /// <summary>
        /// Returns all legal moves on the given position.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="buffer"></param>
        /// <returns>A slice of paramater <paramref name = "buffer"/> that contains the moves generated</returns>
        public static Span<MoveScore> GenerateLegal(Position position, Span<MoveScore> buffer)
        {
            var moves = position.IsCheck() ? GenerateEvasions(position, buffer)
                                           : GenerateNonEvasions(position, buffer);

            Color us = position.ActiveColor;
            Square ksq = position.KingSquare(us);
            Bitboard pinnedPieces = position.BlockersForKing(us) & position.Pieces(us);

            int end = moves.Length;
            for (int i = 0; i < end; i++)
            {
                Move move = buffer[i].Move;
                if (((pinnedPieces.IsOccupied() && pinnedPieces.Contains(move.OriginSquare())) || move.OriginSquare() == ksq || move.Type() == MoveType.EnPassant)
                    && (!position.IsLegal(move)))
                    buffer[i--] = buffer[--end];
            }

            return moves[..end];
        }
        public static Span<MoveScore> GenerateEvasions(Position position, Span<MoveScore> buffer) => Generate(Evasions, position, buffer);
        public static Span<MoveScore> GenerateNonEvasions(Position position, Span<MoveScore> buffer) => Generate(NonEvasions, position, buffer);
        public static Span<MoveScore> GenerateCaptures(Position position, Span<MoveScore> buffer) => Generate(Captures, position, buffer);
        public static Span<MoveScore> GenerateQuiets(Position position, Span<MoveScore> buffer) => Generate(Quiets, position, buffer);

        private static Span<MoveScore> Generate(GenerationType type, Position position, Span<MoveScore> buffer)
        {
            Color us = position.ActiveColor;
            Square ksq = position.KingSquare(us);
            Bitboard occupancy = position.Pieces();

            Bitboard targetSquares = type switch
            {
                Captures => position.Pieces(us.Flip()),
                Quiets => ~position.Pieces(),
                NonEvasions => ~position.Pieces(us),
                _ => Between(ksq, position.Checkers.LeastSignificantSquare()) | position.Checkers
            };

            unsafe
            {
                fixed (MoveScore* fix = buffer)
                {
                    MoveScore* ptr = fix;

                    if (type != Evasions || !position.Checkers.MoreThanOne())
                    {
                        ptr = us == Color.White ? GenerateWhitePawnMoves(type, position, targetSquares, ptr)
                                                : GenerateBlackPawnMoves(type, position, targetSquares, ptr);

                        ptr = GeneratePieceMoves(PieceType.Knight, us, position, targetSquares, occupancy, ptr);
                        ptr = GeneratePieceMoves(PieceType.Bishop, us, position, targetSquares, occupancy, ptr);
                        ptr = GeneratePieceMoves(PieceType.Rook, us, position, targetSquares, occupancy, ptr);
                        ptr = GeneratePieceMoves(PieceType.Queen, us, position, targetSquares, occupancy, ptr);
                    }

                    //King moves
                    Bitboard kingAttacks = Attacks(PieceType.King, ksq) & (type == Evasions ? ~position.Pieces(us) : targetSquares);

                    foreach (Square attack in kingAttacks)
                        *ptr++ = CreateMove(ksq, attack);

                    if (type == Quiets || type == NonEvasions)
                    {
                        CastlingRight ourSide = us == Color.White ? CastlingRight.WhiteSide : CastlingRight.BlackSide;
                        if (position.CanCastle(ourSide))
                        {
                            CastlingRight kingSide = ourSide & CastlingRight.KingSide;
                            if (position.CanCastle(kingSide) && !position.CastlingImpeded(kingSide))
                                *ptr++ = CreateCastlingMove(ksq, position.CastlingRookSquare(kingSide));

                            CastlingRight queenSide = ourSide & CastlingRight.QueenSide;
                            if (position.CanCastle(queenSide) && !position.CastlingImpeded(queenSide))
                                *ptr++ = CreateCastlingMove(ksq, position.CastlingRookSquare(queenSide));
                        }
                    }

                    int movesGenerated = (int)(ptr - fix);

                    return buffer[..movesGenerated];
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe MoveScore* GeneratePromotions(GenerationType type, Square from, Square to, MoveScore* pointer)
        {
            Debug.Assert(false);

            if (type == Captures || type == Evasions || type == NonEvasions)
                *pointer++ = CreatePromotionMove(from, to, PieceType.Queen);

            if (type == Quiets || type == Evasions || type == NonEvasions)
            {
                *pointer++ = CreatePromotionMove(from, to, PieceType.Rook);
                *pointer++ = CreatePromotionMove(from, to, PieceType.Bishop);
                *pointer++ = CreatePromotionMove(from, to, PieceType.Knight);
            }

            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe MoveScore* GenerateWhitePawnMoves(GenerationType type, Position position, Bitboard targets, MoveScore* pointer)
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
                    *pointer++ = CreateMove(to - (int)Up, to);

                foreach (Square to in push2)
                    *pointer++ = CreateMove(to - (int)(Up + (int)Up), to);
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
                    pointer = GeneratePromotions(type, to - (int)Up, to, pointer);

                foreach (Square to in captureRight)
                    pointer = GeneratePromotions(type, to - (int)UpRight, to, pointer);

                foreach (Square to in captureLeft)
                    pointer = GeneratePromotions(type, to - (int)UpLeft, to, pointer);
            }


            //Captures
            if (type == Captures || type == Evasions || type == NonEvasions)
            {
                Bitboard captureRight = pawns.ShiftNorthEast() & enemies;
                Bitboard captureLeft = pawns.ShiftNorthWest() & enemies;

                foreach (Square to in captureRight)
                    *pointer++ = CreateMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    *pointer++ = CreateMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    if (type == Evasions && targets.Contains(position.EnPassantSquare + (int)Up))
                        return pointer;

                    Bitboard epCandidates = pawns & PawnAttacks(enemy, position.EnPassantSquare);

                    foreach (Square from in epCandidates)
                        *pointer++ = CreateEnPassantMove(from, position.EnPassantSquare);
                }
            }

            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe MoveScore* GenerateBlackPawnMoves(GenerationType type, Position position, Bitboard targets, MoveScore* pointer)
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
                    *pointer++ = CreateMove(to - (int)Up, to);

                foreach (Square to in push2)
                    *pointer++ = CreateMove(to - (int)(Up + (int)Up), to);
            }

            //Promotions
            if (promotionPawns != 0)
            {
                Bitboard push = promotionPawns.ShiftSouth() & emptySquares;
                Bitboard captureRight = promotionPawns.ShiftSouthWest() & enemies;
                Bitboard captureLeft = promotionPawns.ShiftSouthEast() & enemies;

                if (type == Evasions)
                    push &= targets;

                foreach (Square to in push)
                    pointer = GeneratePromotions(type, to - (int)Up, to, pointer);

                foreach (Square to in captureRight)
                    pointer = GeneratePromotions(type, to - (int)UpRight, to, pointer);

                foreach (Square to in captureLeft)
                    pointer = GeneratePromotions(type, to - (int)UpLeft, to, pointer);
            }


            //Captures
            if (type == Captures || type == Evasions || type == NonEvasions)
            {
                Bitboard captureRight = pawns.ShiftSouthWest() & enemies;
                Bitboard captureLeft = pawns.ShiftSouthEast() & enemies;

                foreach (Square to in captureRight)
                    *pointer++ = CreateMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    *pointer++ = CreateMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    if (type == Evasions && targets.Contains(position.EnPassantSquare + (int)Up))
                        return pointer;

                    Bitboard epCandidates = pawns & PawnAttacks(enemy, position.EnPassantSquare);

                    foreach (Square from in epCandidates)
                        *pointer++ = CreateEnPassantMove(from, position.EnPassantSquare);
                }
            }

            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe MoveScore* GeneratePieceMoves(PieceType type, Color us, Position position, Bitboard targetSquares, Bitboard occupiedSquares, MoveScore* pointer)
        {
            foreach (Square pieceSquare in position.Pieces(us, type))
                foreach (Square attackSquare in Attacks(type, pieceSquare, occupiedSquares) & targetSquares)
                    *pointer++ = CreateMove(pieceSquare, attackSquare);

            return pointer;
        }
    }
}