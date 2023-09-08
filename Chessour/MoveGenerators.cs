using Chessour.Search;
using static Chessour.Bitboards;
using static Chessour.Color;
using static Chessour.PieceType;
using static Chessour.MoveExtensions;

namespace Chessour
{
    public enum GenerationTypes
    {
        Captures,
        QuietChecks,
        Quiets,
        NonEvasions,
        Evasions,
    }
    
    public static class MoveGenerators
    {
        public const int MAX_MOVE_COUNT = 256;

        public static class Legal
        {
            public static Span<MoveScore> Generate(Position position, Span<MoveScore> buffer)
            {
                var moves = position.IsCheck() ? Evasion.Generate(position, buffer)
                                               : NonEvasion.Generate(position, buffer);

                Color us = position.ActiveColor;
                Square ksq = position.KingSquare(us);
                Bitboard pinnedPieces = position.BlockersForKing(us) & position.Pieces(us);
                int end = moves.Length;

                if (pinnedPieces == 0) //Evaluate pins outside the for loop as it doesnt depends on the move
                {
                    for (int i = 0; i < end; i++)
                    {
                        Move move = buffer[i].Move;
                        if ((move.OriginSquare() == ksq || move.Type() == MoveType.EnPassant)
                            && !position.IsLegal(move))
                            buffer[i--] = buffer[--end];
                    }
                }
                else
                {
                    for (int i = 0; i < end; i++)
                    {
                        Move move = buffer[i].Move;

                        if ((pinnedPieces.Contains(move.OriginSquare()) || move.OriginSquare() == ksq || move.Type() == MoveType.EnPassant)
                            && !position.IsLegal(move))
                            buffer[i--] = buffer[--end];
                    }
                }

                return moves[..end];
            }
        }

        public static class NonEvasion
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Span<MoveScore> Generate(Position position, Span<MoveScore> buffer)
            {
                return position.ActiveColor == Color.White ? White(position, buffer)
                                                           : Black(position, buffer);
            }

            private static Span<MoveScore> White(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.White;
                Square ksq = position.KingSquare(Us);
                Bitboard occupancy = position.Pieces();

                Bitboard targetSquares = ~position.Pieces(Us);

                int generated;
                generated = GenerateWhitePawnMoves(position, buffer, 0);
                generated = GenerateWhiteKnightMoves(position, targetSquares, buffer, generated);
                generated = GenerateWhiteBishopMoves(position, targetSquares, occupancy, buffer, generated);
                generated = GenerateWhiteRookMoves(position, targetSquares, occupancy, buffer, generated);
                generated = GenerateWhiteQueenMoves(position, targetSquares, occupancy, buffer, generated);

                //King moves
                Bitboard kingAttacks = KingAttacks(ksq) & targetSquares;

                foreach (Square attack in kingAttacks)
                    buffer[generated++] = CreateMove(ksq, attack);

                CastlingRight ourSide = Us == Color.White ? CastlingRight.WhiteSide : CastlingRight.BlackSide;
                if (position.CanCastle(ourSide))
                {
                    CastlingRight kingSide = ourSide & CastlingRight.KingSide;
                    if (position.CanCastle(kingSide) && !position.CastlingImpeded(kingSide))
                        buffer[generated++] = CreateCastlingMove(ksq, position.CastlingRookSquare(kingSide));

                    CastlingRight queenSide = ourSide & CastlingRight.QueenSide;
                    if (position.CanCastle(queenSide) && !position.CastlingImpeded(queenSide))
                        buffer[generated++] = CreateCastlingMove(ksq, position.CastlingRookSquare(queenSide));
                }

                return buffer[..generated];
            }

            private static Span<MoveScore> Black(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.Black;
                Square ksq = position.KingSquare(Us);
                Bitboard occupancy = position.Pieces();

                Bitboard targetSquares = ~position.Pieces(Us);

                int generated;
                generated = GenerateBlackPawnMoves(position, buffer, 0);
                generated = GenerateBlackKnightMoves(position, targetSquares, buffer, generated);
                generated = GenerateBlackBishopMoves(position, targetSquares, occupancy, buffer, generated);
                generated = GenerateBlackRookMoves(position, targetSquares, occupancy, buffer, generated);
                generated = GenerateBlackQueenMoves(position, targetSquares, occupancy, buffer, generated);

                //King moves
                Bitboard kingAttacks = KingAttacks(ksq) & targetSquares;

                foreach (Square attack in kingAttacks)
                    buffer[generated++] = CreateMove(ksq, attack);

                CastlingRight ourSide = Us == Color.White ? CastlingRight.WhiteSide : CastlingRight.BlackSide;
                if (position.CanCastle(ourSide))
                {
                    CastlingRight kingSide = ourSide & CastlingRight.KingSide;
                    if (position.CanCastle(kingSide) && !position.CastlingImpeded(kingSide))
                        buffer[generated++] = CreateCastlingMove(ksq, position.CastlingRookSquare(kingSide));

                    CastlingRight queenSide = ourSide & CastlingRight.QueenSide;
                    if (position.CanCastle(queenSide) && !position.CastlingImpeded(queenSide))
                        buffer[generated++] = CreateCastlingMove(ksq, position.CastlingRookSquare(queenSide));
                }

                return buffer[..generated];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GeneratePromotions(Square from, Square to, Span<MoveScore> buffer, int pointer)
            {
                buffer[pointer++] = CreatePromotionMove(from, to, Queen);
                buffer[pointer++] = CreatePromotionMove(from, to, Rook);
                buffer[pointer++] = CreatePromotionMove(from, to, Bishop);
                buffer[pointer++] = CreatePromotionMove(from, to, Knight);
                return pointer;
            }
          
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GenerateWhitePawnMoves(Position position, Span<MoveScore> buffer, int pointer)
            {
                const Color Us = Color.White;
                const Color Enemy = Us == Color.White ? Color.Black : Color.White;

                const Direction Up = Us == Color.White ? Direction.North : Direction.South;
                const Direction UpRight = Us == Color.White ? Direction.NorthEast : Direction.SouthWest;
                const Direction UpLeft = Us == Color.White ? Direction.NorthWest : Direction.SouthEast;

                const Bitboard RelativeRank7 = Us == Color.White ? Bitboard.Rank7 : Bitboard.Rank2;
                const Bitboard RelativeRank3 = Us == Color.White ? Bitboard.Rank3 : Bitboard.Rank6;

                Bitboard emptySquares = ~position.Pieces();
                Bitboard enemies = position.Pieces(Enemy);

                Bitboard pawns = position.Pieces(Us, Pawn) & ~RelativeRank7;
                Bitboard promotionPawns = position.Pieces(Us, Pawn) & RelativeRank7;

                //Pushes except promotions
                Bitboard push1 = pawns.ShiftNorth() & emptySquares;
                Bitboard push2 = (push1 & RelativeRank3).ShiftNorth() & emptySquares;

                foreach (Square to in push1)
                    buffer[pointer++] = CreateMove(to - (int)Up, to);

                foreach (Square to in push2)
                    buffer[pointer++] = CreateMove(to - (int)(Up + (int)Up), to);

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftNorth() & emptySquares;
                    Bitboard promotionRight = promotionPawns.ShiftNorthEast() & enemies;
                    Bitboard promotionLeft= promotionPawns.ShiftNorthWest() & enemies;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, buffer, pointer);

                    foreach (Square to in promotionRight)
                        pointer = GeneratePromotions(to - (int)UpRight, to, buffer, pointer);

                    foreach (Square to in promotionLeft)
                        pointer = GeneratePromotions(to - (int)UpLeft, to, buffer, pointer);
                }

                //Captures
                Bitboard captureRight = pawns.ShiftNorthEast() & enemies;
                Bitboard captureLeft = pawns.ShiftNorthWest() & enemies;

                foreach (Square to in captureRight)
                    buffer[pointer++] = CreateMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    buffer[pointer++] = CreateMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    Bitboard epCandidates = pawns & BlackPawnAttacks(position.EnPassantSquare);

                    foreach (Square from in epCandidates)
                        buffer[pointer++] = CreateEnPassantMove(from, position.EnPassantSquare);
                }

                return pointer;
            }
           
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GenerateBlackPawnMoves(Position position, Span<MoveScore> buffer, int pointer)
            {
                const Color Us = Color.Black;
                const Color Enemy = Us == Color.White ? Color.Black : Color.White;

                const Direction Up = Us == Color.White ? Direction.North : Direction.South;
                const Direction UpRight = Us == Color.White ? Direction.NorthEast : Direction.SouthWest;
                const Direction UpLeft = Us == Color.White ? Direction.NorthWest : Direction.SouthEast;

                const Bitboard RelativeRank7 = Us == Color.White ? Bitboard.Rank7 : Bitboard.Rank2;
                const Bitboard RelativeRank3 = Us == Color.White ? Bitboard.Rank3 : Bitboard.Rank6;

                Bitboard emptySquares = ~position.Pieces();
                Bitboard enemies = position.Pieces(Enemy);

                Bitboard pawns = position.Pieces(Us, Pawn) & ~RelativeRank7;
                Bitboard promotionPawns = position.Pieces(Us, Pawn) & RelativeRank7;

                //Pushes except promotions
                Bitboard push1 = pawns.ShiftSouth() & emptySquares;
                Bitboard push2 = (push1 & RelativeRank3).ShiftSouth() & emptySquares;

                foreach (Square to in push1)
                    buffer[pointer++] = CreateMove(to - (int)Up, to);

                foreach (Square to in push2)
                    buffer[pointer++] = CreateMove(to - (int)(Up + (int)Up), to);

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftSouth() & emptySquares;
                    Bitboard promotionRight = promotionPawns.ShiftSouthWest() & enemies;
                    Bitboard promotionLeft = promotionPawns.ShiftSouthEast() & enemies;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, buffer, pointer);

                    foreach (Square to in promotionRight)
                        pointer = GeneratePromotions(to - (int)UpRight, to, buffer, pointer);

                    foreach (Square to in promotionLeft)
                        pointer = GeneratePromotions(to - (int)UpLeft, to, buffer, pointer);
                }


                //Captures
                Bitboard captureRight = pawns.ShiftSouthWest() & enemies;
                Bitboard captureLeft = pawns.ShiftSouthEast() & enemies;

                foreach (Square to in captureRight)
                    buffer[pointer++] = CreateMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    buffer[pointer++] = CreateMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    Bitboard epCandidate = pawns & WhitePawnAttacks(position.EnPassantSquare);

                    foreach (Square from in epCandidate)
                        buffer[pointer++] = CreateEnPassantMove(from, position.EnPassantSquare);
                }

                return pointer;
            }
        }

        public static class Evasion
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Span<MoveScore> Generate(Position position, Span<MoveScore> buffer)
            {
                return position.ActiveColor == Color.White ? White(position, buffer)
                                                            : Black(position, buffer);
            }

            private static Span<MoveScore> White(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.White;
                
                Square ksq = position.KingSquare(Us);

                int generated = 0;

                if (!position.Checkers.MoreThanOne())
                {
                    Bitboard occupancy = position.Pieces();
                    Bitboard targetSquares = Between(ksq, position.Checkers.LeastSignificantSquare()) | position.Checkers;

                    generated = GenerateWhitePawnMoves(position, targetSquares, buffer, generated);
                    generated = GenerateWhiteKnightMoves(position, targetSquares, buffer, generated);
                    generated = GenerateWhiteBishopMoves(position, targetSquares, occupancy, buffer, generated);
                    generated = GenerateWhiteRookMoves(position, targetSquares, occupancy, buffer, generated);
                    generated = GenerateWhiteQueenMoves(position, targetSquares, occupancy, buffer, generated);
                }

                //King moves
                Bitboard kingAttacks = KingAttacks(ksq) & ~position.Pieces(Us);

                foreach (Square attack in kingAttacks)
                    buffer[generated++] = CreateMove(ksq, attack);

                return buffer[..generated];
            }

            private static Span<MoveScore> Black(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.Black;

                Square ksq = position.KingSquare(Us);

                int generated = 0;
                if (!position.Checkers.MoreThanOne())
                {
                    Bitboard occupancy = position.Pieces();
                    Bitboard targetSquares = Between(ksq, position.Checkers.LeastSignificantSquare()) | position.Checkers;

                    generated = GenerateBlackPawnMoves(position, targetSquares, buffer, generated);
                    generated = GenerateBlackKnightMoves(position, targetSquares, buffer, generated);
                    generated = GenerateBlackBishopMoves(position, targetSquares, occupancy, buffer, generated);
                    generated = GenerateBlackRookMoves(position, targetSquares, occupancy, buffer, generated);
                    generated = GenerateBlackQueenMoves(position, targetSquares, occupancy, buffer, generated);
                }

                //King moves
                Bitboard kingAttacks = KingAttacks(ksq) & ~position.Pieces(Us);

                foreach (Square attack in kingAttacks)
                    buffer[generated++] = CreateMove(ksq, attack);

                return buffer[..generated];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GeneratePromotions(Square from, Square to, Span<MoveScore> buffer, int pointer)
            {
                buffer[pointer++] = CreatePromotionMove(from, to, Queen);
                buffer[pointer++] = CreatePromotionMove(from, to, Rook);
                buffer[pointer++] = CreatePromotionMove(from, to, Bishop);
                buffer[pointer++] = CreatePromotionMove(from, to, Knight);
                return pointer;
            }
           
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GenerateWhitePawnMoves(Position position, Bitboard targets, Span<MoveScore> buffer, int pointer)
            {
                const Color Us = Color.White;

                const Direction Up = Us == Color.White ? Direction.North : Direction.South;
                const Direction UpRight = Us == Color.White ? Direction.NorthEast : Direction.SouthWest;
                const Direction UpLeft = Us == Color.White ? Direction.NorthWest : Direction.SouthEast;

                const Bitboard RelativeRank7 = Us == Color.White ? Bitboard.Rank7 : Bitboard.Rank2;
                const Bitboard RelativeRank3 = Us == Color.White ? Bitboard.Rank3 : Bitboard.Rank6;

                Bitboard emptySquares = ~position.Pieces();
                Bitboard enemies = position.Checkers;

                Bitboard pawns = position.Pieces(Us, Pawn) & ~RelativeRank7;
                Bitboard promotionPawns = position.Pieces(Us, Pawn) & RelativeRank7;

                //Pushes except promotions
                Bitboard push1 = pawns.ShiftNorth() & emptySquares;
                Bitboard push2 = (push1 & RelativeRank3).ShiftNorth() & emptySquares;

                push1 &= targets;
                push2 &= targets;

                foreach (Square to in push1)
                    buffer[pointer++] = CreateMove(to - (int)Up, to);

                foreach (Square to in push2)
                    buffer[pointer++] = CreateMove(to - (int)(Up + (int)Up), to);

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftNorth() & emptySquares;                
                    Bitboard promotionRight = promotionPawns.ShiftNorthEast() & enemies;
                    Bitboard promotionLeft = promotionPawns.ShiftNorthWest() & enemies;

                    promotionPush &= targets;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, buffer, pointer);

                    foreach (Square to in promotionRight)
                        pointer = GeneratePromotions(to - (int)UpRight, to, buffer, pointer);

                    foreach (Square to in promotionLeft)
                        pointer = GeneratePromotions(to - (int)UpLeft, to, buffer, pointer);
                }


                //Captures
                Bitboard captureRight = pawns.ShiftNorthEast() & enemies;
                Bitboard captureLeft = pawns.ShiftNorthWest() & enemies;

                foreach (Square to in captureRight)
                    buffer[pointer++] = CreateMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    buffer[pointer++] = CreateMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    if (targets.Contains(position.EnPassantSquare + (int)Up))
                        return pointer;

                    Bitboard epCandidate = pawns & BlackPawnAttacks(position.EnPassantSquare);

                    foreach (Square from in epCandidate)
                        buffer[pointer++] = CreateEnPassantMove(from, position.EnPassantSquare);
                }

                return pointer;
            }
           
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GenerateBlackPawnMoves(Position position, Bitboard targets, Span<MoveScore> buffer, int pointer)
            {
                const Color Us = Color.Black;

                const Direction Up = Us == Color.White ? Direction.North : Direction.South;
                const Direction UpRight = Us == Color.White ? Direction.NorthEast : Direction.SouthWest;
                const Direction UpLeft = Us == Color.White ? Direction.NorthWest : Direction.SouthEast;

                const Bitboard RelativeRank7 = Us == Color.White ? Bitboard.Rank7 : Bitboard.Rank2;
                const Bitboard RelativeRank3 = Us == Color.White ? Bitboard.Rank3 : Bitboard.Rank6;

                Bitboard emptySquares = ~position.Pieces();
                Bitboard enemies = position.Checkers;

                Bitboard pawns = position.Pieces(Us, Pawn) & ~RelativeRank7;
                Bitboard promotionPawns = position.Pieces(Us, Pawn) & RelativeRank7;

                //Pushes except promotions
                Bitboard push1 = pawns.ShiftSouth() & emptySquares;
                Bitboard push2 = (push1 & RelativeRank3).ShiftSouth() & emptySquares;

                push1 &= targets;
                push2 &= targets;

                foreach (Square to in push1)
                    buffer[pointer++] = CreateMove(to - (int)Up, to);

                foreach (Square to in push2)
                    buffer[pointer++] = CreateMove(to - (int)(Up + (int)Up), to);

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftSouth() & emptySquares;
                    Bitboard promotionRight = promotionPawns.ShiftSouthWest() & enemies;
                    Bitboard promotionLeft = promotionPawns.ShiftSouthEast() & enemies;

                    promotionPush &= targets;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, buffer, pointer);

                    foreach (Square to in promotionRight)
                        pointer = GeneratePromotions(to - (int)UpRight, to, buffer, pointer);

                    foreach (Square to in promotionLeft)
                        pointer = GeneratePromotions(to - (int)UpLeft, to, buffer, pointer);
                }


                //Captures
                Bitboard captureRight = pawns.ShiftSouthWest() & enemies;
                Bitboard captureLeft = pawns.ShiftSouthEast() & enemies;

                foreach (Square to in captureRight)
                    buffer[pointer++] = CreateMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    buffer[pointer++] = CreateMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    if (targets.Contains(position.EnPassantSquare + (int)Up))
                        return pointer;

                    Bitboard epCandidate = pawns & WhitePawnAttacks(position.EnPassantSquare);

                    foreach (Square from in epCandidate)
                        buffer[pointer++] = CreateEnPassantMove(from, position.EnPassantSquare);
                }

                return pointer;
            }
        }

        public static class Capture
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Span<MoveScore> Generate(Position position, Span<MoveScore> buffer)
            {
                return position.ActiveColor == Color.White ? White(position, buffer)
                                                           : Black(position, buffer);
            }

            private static Span<MoveScore> White(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.White;
                const Color Enemy = Us == Color.White ? Color.Black : Color.White;
                Square ksq = position.KingSquare(Us);
                Bitboard occupancy = position.Pieces();
                Bitboard targetSquares = position.Pieces(Enemy);

                int generated;
                generated = GenerateWhitePawnMoves(position, buffer, 0);
                generated = GenerateWhiteKnightMoves(position, targetSquares, buffer, generated);
                generated = GenerateWhiteBishopMoves(position, targetSquares, occupancy, buffer, generated);
                generated = GenerateWhiteRookMoves(position, targetSquares, occupancy, buffer, generated);
                generated = GenerateWhiteQueenMoves(position, targetSquares, occupancy, buffer, generated);

                //King moves
                Bitboard kingAttacks = KingAttacks(ksq) & targetSquares;

                foreach (Square attack in kingAttacks)
                    buffer[generated++] = CreateMove(ksq, attack);

                return buffer[..generated];
            }

            private static Span<MoveScore> Black(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.Black;
                const Color Enemy = Us == Color.White ? Color.Black : Color.White;

                Square ksq = position.KingSquare(Us);
                Bitboard occupancy = position.Pieces();
                Bitboard targetSquares = position.Pieces(Enemy);

                int generated;
                generated = GenerateBlackPawnMoves(position, buffer, 0);
                generated = GenerateBlackKnightMoves(position, targetSquares, buffer, generated);
                generated = GenerateBlackBishopMoves(position, targetSquares, occupancy, buffer, generated);
                generated = GenerateBlackRookMoves(position, targetSquares, occupancy, buffer, generated);
                generated = GenerateBlackQueenMoves(position, targetSquares, occupancy, buffer, generated);

                //King moves
                Bitboard kingAttacks = KingAttacks(ksq) & targetSquares;

                foreach (Square attack in kingAttacks)
                    buffer[generated++] = CreateMove(ksq, attack);

                return buffer[..generated];           
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GeneratePromotions(Square from, Square to, Span<MoveScore> buffer, int pointer)
            {
                buffer[pointer++] = CreatePromotionMove(from, to, Queen);
                return pointer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GenerateCapturePromotions(Square from, Square to, Span<MoveScore> buffer, int pointer)
            {
                buffer[pointer++] = CreatePromotionMove(from, to, Queen);
                buffer[pointer++] = CreatePromotionMove(from, to, Rook);
                buffer[pointer++] = CreatePromotionMove(from, to, Bishop);
                buffer[pointer++] = CreatePromotionMove(from, to, Knight);
                return pointer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GenerateWhitePawnMoves(Position position, Span<MoveScore> buffer, int pointer)
            {
                const Color Us = Color.White;
                const Color Enemy = Us == Color.White ? Color.Black : Color.White;

                const Direction Up = Us == Color.White ? Direction.North : Direction.South;
                const Direction UpRight = Us == Color.White ? Direction.NorthEast : Direction.SouthWest;
                const Direction UpLeft = Us == Color.White ? Direction.NorthWest : Direction.SouthEast;

                const Bitboard RelativeRank7 = Us == Color.White ? Bitboard.Rank7 : Bitboard.Rank2;

                Bitboard emptySquares = ~position.Pieces();
                Bitboard enemies = position.Pieces(Enemy);

                Bitboard pawns = position.Pieces(Us, Pawn) & ~RelativeRank7;
                Bitboard promotionPawns = position.Pieces(Us, Pawn) & RelativeRank7;

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftNorth() & emptySquares;
                    Bitboard promotionRight = promotionPawns.ShiftNorthEast() & enemies;
                    Bitboard promotionLeft = promotionPawns.ShiftNorthWest() & enemies;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, buffer, pointer);

                    foreach (Square to in promotionRight)
                        pointer = GenerateCapturePromotions(to - (int)UpRight, to, buffer, pointer);

                    foreach (Square to in promotionLeft)
                        pointer = GenerateCapturePromotions(to - (int)UpLeft, to, buffer, pointer);
                }

                //Captures
                Bitboard captureRight = pawns.ShiftNorthEast() & enemies;
                Bitboard captureLeft = pawns.ShiftNorthWest() & enemies;

                foreach (Square to in captureRight)
                    buffer[pointer++] = CreateMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    buffer[pointer++] = CreateMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    Bitboard epCandidates = pawns & BlackPawnAttacks(position.EnPassantSquare);

                    foreach (Square from in epCandidates)
                        buffer[pointer++] = CreateEnPassantMove(from, position.EnPassantSquare);
                }

                return pointer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GenerateBlackPawnMoves(Position position, Span<MoveScore> buffer, int pointer)
            {
                const Color Us = Color.Black;
                const Color Enemy = Us == Color.White ? Color.Black : Color.White;

                const Direction Up = Us == Color.White ? Direction.North : Direction.South;
                const Direction UpRight = Us == Color.White ? Direction.NorthEast : Direction.SouthWest;
                const Direction UpLeft = Us == Color.White ? Direction.NorthWest : Direction.SouthEast;

                const Bitboard RelativeRank7 = Us == Color.White ? Bitboard.Rank7 : Bitboard.Rank2;

                Bitboard emptySquares = ~position.Pieces();
                Bitboard enemies = position.Pieces(Enemy);

                Bitboard pawns = position.Pieces(Us, Pawn) & ~RelativeRank7;
                Bitboard promotionPawns = position.Pieces(Us, Pawn) & RelativeRank7;

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftSouth() & emptySquares;
                    Bitboard promotionRight = promotionPawns.ShiftSouthWest() & enemies;
                    Bitboard promotionLeft = promotionPawns.ShiftSouthEast() & enemies;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, buffer, pointer);

                    foreach (Square to in promotionRight)
                        pointer = GenerateCapturePromotions(to - (int)UpRight, to, buffer, pointer);

                    foreach (Square to in promotionLeft)
                        pointer = GenerateCapturePromotions(to - (int)UpLeft, to, buffer, pointer);
                }


                //Captures
                Bitboard captureRight = pawns.ShiftSouthWest() & enemies;
                Bitboard captureLeft = pawns.ShiftSouthEast() & enemies;

                foreach (Square to in captureRight)
                    buffer[pointer++] = CreateMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    buffer[pointer++] = CreateMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    Bitboard epCandidate = pawns & WhitePawnAttacks(position.EnPassantSquare);

                    foreach (Square from in epCandidate)
                        buffer[pointer++] = CreateEnPassantMove(from, position.EnPassantSquare);
                }

                return pointer;
            }
        }

        public static class Quiet
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static  Span<MoveScore> Generate(Position position, Span<MoveScore> buffer)
            {
                return position.ActiveColor == Color.White ? White(position, buffer)
                                                           : Black(position, buffer);
            }

            private static Span<MoveScore> White(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.White;
                Square ksq = position.KingSquare(Us);
                Bitboard occupancy = position.Pieces();

                Bitboard targetSquares = ~position.Pieces();

                int generated;
                generated = GenerateWhitePawnMoves(position, buffer, 0);
                generated = GenerateWhiteKnightMoves(position, targetSquares, buffer, generated);
                generated = GenerateWhiteBishopMoves(position, targetSquares, occupancy, buffer, generated);
                generated = GenerateWhiteRookMoves(position, targetSquares, occupancy, buffer, generated);
                generated = GenerateWhiteQueenMoves(position, targetSquares, occupancy, buffer, generated);

                //King moves
                Bitboard kingAttacks = KingAttacks(ksq) & targetSquares;

                foreach (Square attack in kingAttacks)
                    buffer[generated++] = CreateMove(ksq, attack);

                CastlingRight ourSide = Us == Color.White ? CastlingRight.WhiteSide : CastlingRight.BlackSide;
                if (position.CanCastle(ourSide))
                {
                    CastlingRight kingSide = ourSide & CastlingRight.KingSide;
                    if (position.CanCastle(kingSide) && !position.CastlingImpeded(kingSide))
                        buffer[generated++] = CreateCastlingMove(ksq, position.CastlingRookSquare(kingSide));

                    CastlingRight queenSide = ourSide & CastlingRight.QueenSide;
                    if (position.CanCastle(queenSide) && !position.CastlingImpeded(queenSide))
                        buffer[generated++] = CreateCastlingMove(ksq, position.CastlingRookSquare(queenSide));
                }

                return buffer[..generated];
            }

            private static Span<MoveScore> Black(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.Black;
                Square ksq = position.KingSquare(Us);
                Bitboard occupancy = position.Pieces();

                Bitboard targetSquares = ~position.Pieces();

                int generated;
                generated = GenerateBlackPawnMoves(position, buffer, 0);
                generated = GenerateBlackKnightMoves(position, targetSquares, buffer, generated);
                generated = GenerateBlackBishopMoves(position, targetSquares, occupancy, buffer, generated);
                generated = GenerateBlackRookMoves(position, targetSquares, occupancy, buffer, generated);
                generated = GenerateBlackQueenMoves(position, targetSquares, occupancy, buffer, generated);

                //King moves
                Bitboard kingAttacks = KingAttacks(ksq) & targetSquares;

                foreach (Square attack in kingAttacks)
                    buffer[generated++] = CreateMove(ksq, attack);

                CastlingRight ourSide = Us == Color.White ? CastlingRight.WhiteSide : CastlingRight.BlackSide;
                if (position.CanCastle(ourSide))
                {
                    CastlingRight kingSide = ourSide & CastlingRight.KingSide;
                    if (position.CanCastle(kingSide) && !position.CastlingImpeded(kingSide))
                        buffer[generated++] = CreateCastlingMove(ksq, position.CastlingRookSquare(kingSide));

                    CastlingRight queenSide = ourSide & CastlingRight.QueenSide;
                    if (position.CanCastle(queenSide) && !position.CastlingImpeded(queenSide))
                        buffer[generated++] = CreateCastlingMove(ksq, position.CastlingRookSquare(queenSide));
                }

                return buffer[..generated];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GeneratePromotions(Square from, Square to, Span<MoveScore> buffer, int pointer)
            {
                buffer[pointer++] = CreatePromotionMove(from, to, Rook);
                buffer[pointer++] = CreatePromotionMove(from, to, Bishop);
                buffer[pointer++] = CreatePromotionMove(from, to, Knight);
                return pointer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GenerateWhitePawnMoves(Position position, Span<MoveScore> buffer, int pointer)
            {
                const Color Us = Color.White;
                const Direction Up = Us == Color.White ? Direction.North : Direction.South;
                const Bitboard RelativeRank7 = Us == Color.White ? Bitboard.Rank7 : Bitboard.Rank2;
                const Bitboard RelativeRank3 = Us == Color.White ? Bitboard.Rank3 : Bitboard.Rank6;

                Bitboard emptySquares = ~position.Pieces();

                Bitboard pawns = position.Pieces(Us, Pawn) & ~RelativeRank7;
                Bitboard promotionPawns = position.Pieces(Us, Pawn) & RelativeRank7;

                //Pushes except promotions
                Bitboard push1 = pawns.ShiftNorth() & emptySquares;
                Bitboard push2 = (push1 & RelativeRank3).ShiftNorth() & emptySquares;

                foreach (Square to in push1)
                    buffer[pointer++] = CreateMove(to - (int)Up, to);

                foreach (Square to in push2)
                    buffer[pointer++] = CreateMove(to - (int)(Up + (int)Up), to);

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftNorth() & emptySquares;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, buffer, pointer);
                }

                return pointer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GenerateBlackPawnMoves(Position position, Span<MoveScore> buffer, int pointer)
            {
                const Color Us = Color.Black;
                const Direction Up = Us == Color.White ? Direction.North : Direction.South;
                const Bitboard RelativeRank7 = Us == Color.White ? Bitboard.Rank7 : Bitboard.Rank2;
                const Bitboard RelativeRank3 = Us == Color.White ? Bitboard.Rank3 : Bitboard.Rank6;

                Bitboard emptySquares = ~position.Pieces();

                Bitboard pawns = position.Pieces(Us, Pawn) & ~RelativeRank7;
                Bitboard promotionPawns = position.Pieces(Us, Pawn) & RelativeRank7;

                //Pushes except promotions
                Bitboard push1 = pawns.ShiftSouth() & emptySquares;
                Bitboard push2 = (push1 & RelativeRank3).ShiftSouth() & emptySquares;

                foreach (Square to in push1)
                    buffer[pointer++] = CreateMove(to - (int)Up, to);

                foreach (Square to in push2)
                    buffer[pointer++] = CreateMove(to - (int)(Up + (int)Up), to);

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftSouth() & emptySquares;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, buffer, pointer);
                }
             
                return pointer;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GenerateWhiteKnightMoves(Position position, Bitboard targetSquares, Span<MoveScore> buffer, int pointer)
        {
            foreach (Square pieceSquare in position.Pieces(White, Knight)) 
            {
                Bitboard attacks = KnightAttacks(pieceSquare) & targetSquares;

                foreach (Square attackSquare in attacks)
                    buffer[pointer++] = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GenerateBlackKnightMoves(Position position, Bitboard targetSquares, Span<MoveScore> buffer, int pointer)
        {
            foreach (Square pieceSquare in position.Pieces(Black, Knight))
            {
                Bitboard attacks = KnightAttacks(pieceSquare) & targetSquares;

                foreach (Square attackSquare in attacks)
                    buffer[pointer++] = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GenerateWhiteBishopMoves(Position position, Bitboard targetSquares, Bitboard occupiedSquares, Span<MoveScore> buffer, int pointer)
        {
            foreach (Square pieceSquare in position.Pieces(White, Bishop))
            {
                Bitboard attacks = BishopAttacks(pieceSquare, occupiedSquares) & targetSquares;

                foreach (Square attackSquare in attacks)
                    buffer[pointer++] = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GenerateBlackBishopMoves(Position position, Bitboard targetSquares, Bitboard occupiedSquares, Span<MoveScore> buffer, int pointer)
        {
            foreach (Square pieceSquare in position.Pieces(Black, Bishop))
            {
                Bitboard attacks = BishopAttacks(pieceSquare, occupiedSquares) & targetSquares;

                foreach (Square attackSquare in attacks)
                    buffer[pointer++] = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GenerateWhiteRookMoves(Position position, Bitboard targetSquares, Bitboard occupiedSquares, Span<MoveScore> buffer, int pointer)
        {
            foreach (Square pieceSquare in position.Pieces(White, Rook))
            {
                Bitboard attacks = RookAttacks(pieceSquare, occupiedSquares) & targetSquares;

                foreach (Square attackSquare in attacks)
                    buffer[pointer++] = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GenerateBlackRookMoves(Position position, Bitboard targetSquares, Bitboard occupiedSquares, Span<MoveScore> buffer, int pointer)
        {
            foreach (Square pieceSquare in position.Pieces(Black, Rook))
            {
                Bitboard attacks = RookAttacks(pieceSquare, occupiedSquares) & targetSquares;

                foreach (Square attackSquare in attacks)
                    buffer[pointer++] = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GenerateWhiteQueenMoves(Position position, Bitboard targetSquares, Bitboard occupiedSquares, Span<MoveScore> buffer, int pointer)
        {
            foreach (Square pieceSquare in position.Pieces(White, Queen))
            {
                Bitboard attacks = QueenAttacks(pieceSquare, occupiedSquares) & targetSquares;

                foreach (Square attackSquare in attacks)
                    buffer[pointer++] = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GenerateBlackQueenMoves(Position position, Bitboard targetSquares, Bitboard occupiedSquares, Span<MoveScore> buffer, int pointer)
        {
            foreach (Square pieceSquare in position.Pieces(Black, Queen))
            {
                Bitboard attacks = QueenAttacks(pieceSquare, occupiedSquares) & targetSquares;

                foreach (Square attackSquare in attacks)
                    buffer[pointer++] = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }
       }
}