using Chessour.Search;
using static Chessour.Bitboards;
using static Chessour.BoardRepresentation;
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

    public static class MoveGenerator
    {
        public const int MAX_MOVE_COUNT = 256;

        public static Span<MoveScore> Generate(Position position, Span<MoveScore> buffer)
        {
            var moves = position.IsCheck() ? Generate(Evasions, position, buffer)
                                           : Generate(NonEvasions, position, buffer);

            Color us = position.ActiveColor;
            Square ksq = position.KingSquare(us);
            Bitboard pinnedPieces = position.BlockersForKing(us) & position.Pieces(us);

            int end = moves.Length;
            for (int i = 0; i < end; i++)
            {
                Move move = buffer[i].Move;
                if (((pinnedPieces.IsOccupied() && pinnedPieces.Contains(move.From())) || move.From() == ksq || move.Type() == MoveType.EnPassant)
                    && (!position.IsLegal(move)))
                    buffer[i--] = buffer[--end];
            }

            return moves[..end];
        }

        public static Span<MoveScore> Generate(GenerationType type, Position position, Span<MoveScore> buffer)
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

            int movesGenerated = 0;
            if (type != Evasions || !position.Checkers.MoreThanOne())
            {
                if (us == Color.White)
                    GenerateWhitePawnMoves(type, position, targetSquares, buffer, ref movesGenerated);
                else
                    GenerateBlackPawnMoves(type, position, targetSquares, buffer, ref movesGenerated);

                GeneratePieceMoves(PieceType.Knight, us, position, targetSquares, occupancy, buffer, ref movesGenerated);
                GeneratePieceMoves(PieceType.Bishop, us, position, targetSquares, occupancy, buffer, ref movesGenerated);
                GeneratePieceMoves(PieceType.Rook, us, position, targetSquares, occupancy, buffer, ref movesGenerated);
                GeneratePieceMoves(PieceType.Queen, us, position, targetSquares, occupancy, buffer, ref movesGenerated);
            }

            //King moves
            Bitboard kingAttacks = Attacks(PieceType.King, ksq) & (type == Evasions ? ~position.Pieces(us) : targetSquares);

            foreach (Square attack in kingAttacks)
                buffer[movesGenerated++] = MakeMove(ksq, attack);

            if (type == Quiets || type == NonEvasions)
            {
                CastlingRight ourSide = us == Color.White ? CastlingRight.WhiteSide : CastlingRight.BlackSide;
                if (position.CanCastle(ourSide))
                {
                    CastlingRight kingSide = ourSide & CastlingRight.KingSide;
                    if (position.CanCastle(kingSide) && !position.CastlingImpeded(kingSide))
                        buffer[movesGenerated++] = MakeCastlingMove(ksq, position.CastlingRookSquare(kingSide));

                    CastlingRight queenSide = ourSide & CastlingRight.QueenSide;
                    if (position.CanCastle(queenSide) && !position.CastlingImpeded(queenSide))
                        buffer[movesGenerated++] = MakeCastlingMove(ksq, position.CastlingRookSquare(queenSide));
                }
            }

            return buffer[..movesGenerated];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GeneratePromotions(GenerationType type, Square from, Square to, Span<MoveScore> buffer, ref int pointer)
        {
            if (type == Captures || type == Evasions || type == NonEvasions)
                buffer[pointer++] = MakePromotionMove(from, to, PieceType.Queen);

            if (type == Quiets || type == Evasions || type == NonEvasions)
            {
                buffer[pointer++] = MakePromotionMove(from, to, PieceType.Rook);
                buffer[pointer++] = MakePromotionMove(from, to, PieceType.Bishop);
                buffer[pointer++] = MakePromotionMove(from, to, PieceType.Knight);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GenerateWhitePawnMoves(GenerationType type, Position position, Bitboard targets, Span<MoveScore> buffer, ref int pointer)
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
                    buffer[pointer++] = MakeMove(to - (int)Up, to);

                foreach (Square to in push2)
                    buffer[pointer++] = MakeMove(to - (int)(Up + (int)Up), to);
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
                    GeneratePromotions(type, to - (int)Up, to, buffer, ref pointer);

                foreach (Square to in captureRight)
                    GeneratePromotions(type, to - (int)UpRight, to, buffer, ref pointer);

                foreach (Square to in captureLeft)
                    GeneratePromotions(type, to - (int)UpLeft, to, buffer, ref pointer);
            }


            //Captures
            if (type == Captures || type == Evasions || type == NonEvasions)
            {
                Bitboard captureRight = pawns.ShiftNorthEast() & enemies;
                Bitboard captureLeft = pawns.ShiftNorthWest() & enemies;

                foreach (Square to in captureRight)
                    buffer[pointer++] = MakeMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    buffer[pointer++] = MakeMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    if (type == Evasions && targets.Contains(position.EnPassantSquare + (int)Up))
                        return;

                    Bitboard epCandidates = pawns & PawnAttacks(enemy, position.EnPassantSquare);

                    foreach (Square from in epCandidates)
                        buffer[pointer++] = MakeEnpassantMove(from, position.EnPassantSquare);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GenerateBlackPawnMoves(GenerationType type, Position position, Bitboard targets, Span<MoveScore> buffer, ref int pointer)
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
                    buffer[pointer++] = MakeMove(to - (int)Up, to);

                foreach (Square to in push2)
                    buffer[pointer++] = MakeMove(to - (int)(Up + (int)Up), to);
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
                    GeneratePromotions(type, to - (int)Up, to, buffer, ref pointer);

                foreach (Square to in captureRight)
                    GeneratePromotions(type, to - (int)UpRight, to, buffer, ref pointer);

                foreach (Square to in captureLeft)
                    GeneratePromotions(type, to - (int)UpLeft, to, buffer, ref pointer);
            }


            //Captures
            if (type == Captures || type == Evasions || type == NonEvasions)
            {
                Bitboard captureRight = pawns.ShiftSouthWest() & enemies;
                Bitboard captureLeft = pawns.ShiftSouthEast() & enemies;

                foreach (Square to in captureRight)
                    buffer[pointer++] = MakeMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    buffer[pointer++] = MakeMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    if (type == Evasions && targets.Contains(position.EnPassantSquare + (int)Up))
                        return;

                    Bitboard epCandidates = pawns & PawnAttacks(enemy, position.EnPassantSquare);

                    foreach (Square from in epCandidates)
                        buffer[pointer++] = MakeEnpassantMove(from, position.EnPassantSquare);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GeneratePieceMoves(PieceType type, Color us, Position position, Bitboard targetSquares, Bitboard occupiedSquares, Span<MoveScore> buffer, ref int pointer)
        {
            foreach (Square pieceSquare in position.Pieces(us, type))
                foreach (Square attackSquare in Attacks(type, pieceSquare, occupiedSquares) & targetSquares)
                    buffer[pointer++] = MakeMove(pieceSquare, attackSquare);
        }
    }
}