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

            private static unsafe Span<MoveScore> White(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.White;
                Square ksq = position.KingSquare(Us);
                Bitboard occupancy = position.Pieces();

                Bitboard targetSquares = ~position.Pieces(Us);

                fixed (MoveScore* fix = buffer)
                {
                    MoveScore* ptr = fix;

                    ptr = GenerateWhitePawnMoves(position, targetSquares, ptr);
                    ptr = GenerateWhiteKnightMoves(position, targetSquares, ptr);
                    ptr = GenerateWhiteBishopMoves(position, targetSquares, occupancy, ptr);
                    ptr = GenerateWhiteRookMoves(position, targetSquares, occupancy, ptr);
                    ptr = GenerateWhiteQueenMoves(position, targetSquares, occupancy, ptr);

                    //King moves
                    Bitboard kingAttacks = KingAttacks(ksq) & targetSquares;

                    foreach (Square attack in kingAttacks)
                        *ptr++ = CreateMove(ksq, attack);

                    CastlingRight ourSide = Us == Color.White ? CastlingRight.WhiteSide : CastlingRight.BlackSide;
                    if (position.CanCastle(ourSide))
                    {
                        CastlingRight kingSide = ourSide & CastlingRight.KingSide;
                        if (position.CanCastle(kingSide) && !position.CastlingImpeded(kingSide))
                            *ptr++ = CreateCastlingMove(ksq, position.CastlingRookSquare(kingSide));

                        CastlingRight queenSide = ourSide & CastlingRight.QueenSide;
                        if (position.CanCastle(queenSide) && !position.CastlingImpeded(queenSide))
                            *ptr++ = CreateCastlingMove(ksq, position.CastlingRookSquare(queenSide));
                    }

                    int movesGenerated = (int)(ptr - fix);

                    return buffer[..movesGenerated];
                }          
            }

            private static unsafe Span<MoveScore> Black(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.Black;
                Square ksq = position.KingSquare(Us);
                Bitboard occupancy = position.Pieces();

                Bitboard targetSquares = ~position.Pieces(Us);

                fixed (MoveScore* fix = buffer)
                {
                    MoveScore* ptr = fix;

                    ptr = GenerateBlackPawnMoves(position, targetSquares, ptr);
                    ptr = GenerateBlackKnightMoves(position, targetSquares, ptr);
                    ptr = GenerateBlackBishopMoves(position, targetSquares, occupancy, ptr);
                    ptr = GenerateBlackRookMoves(position, targetSquares, occupancy, ptr);
                    ptr = GenerateBlackQueenMoves(position, targetSquares, occupancy, ptr);

                    //King moves
                    Bitboard kingAttacks = KingAttacks(ksq) & targetSquares;

                    foreach (Square attack in kingAttacks)
                        *ptr++ = CreateMove(ksq, attack);

                    CastlingRight ourSide = Us == Color.White ? CastlingRight.WhiteSide : CastlingRight.BlackSide;
                    if (position.CanCastle(ourSide))
                    {
                        CastlingRight kingSide = ourSide & CastlingRight.KingSide;
                        if (position.CanCastle(kingSide) && !position.CastlingImpeded(kingSide))
                            *ptr++ = CreateCastlingMove(ksq, position.CastlingRookSquare(kingSide));

                        CastlingRight queenSide = ourSide & CastlingRight.QueenSide;
                        if (position.CanCastle(queenSide) && !position.CastlingImpeded(queenSide))
                            *ptr++ = CreateCastlingMove(ksq, position.CastlingRookSquare(queenSide));
                    }

                    int movesGenerated = (int)(ptr - fix);

                    return buffer[..movesGenerated];
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe MoveScore* GeneratePromotions(Square from, Square to, MoveScore* pointer)
            {
                *pointer++ = CreatePromotionMove(from, to, PieceType.Queen);
                *pointer++ = CreatePromotionMove(from, to, PieceType.Rook);
                *pointer++ = CreatePromotionMove(from, to, PieceType.Bishop);
                *pointer++ = CreatePromotionMove(from, to, PieceType.Knight);

                return pointer;
            }
          
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe MoveScore* GenerateWhitePawnMoves(Position position, Bitboard targets, MoveScore* pointer)
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
                    *pointer++ = CreateMove(to - (int)Up, to);

                foreach (Square to in push2)
                    *pointer++ = CreateMove(to - (int)(Up + (int)Up), to);

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftNorth() & emptySquares;
                    Bitboard promotionRight = promotionPawns.ShiftNorthEast() & enemies;
                    Bitboard promotionLeft= promotionPawns.ShiftNorthWest() & enemies;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, pointer);

                    foreach (Square to in promotionRight)
                        pointer = GeneratePromotions(to - (int)UpRight, to, pointer);

                    foreach (Square to in promotionLeft)
                        pointer = GeneratePromotions(to - (int)UpLeft, to, pointer);
                }


                //Captures
                Bitboard captureRight = pawns.ShiftNorthEast() & enemies;
                Bitboard captureLeft = pawns.ShiftNorthWest() & enemies;

                foreach (Square to in captureRight)
                    *pointer++ = CreateMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    *pointer++ = CreateMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    Bitboard epCandidates = pawns & BlackPawnAttacks(position.EnPassantSquare);

                    foreach (Square from in epCandidates)
                        *pointer++ = CreateEnPassantMove(from, position.EnPassantSquare);
                }

                return pointer;
            }
           
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe MoveScore* GenerateBlackPawnMoves(Position position, Bitboard targets, MoveScore* pointer)
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
                    *pointer++ = CreateMove(to - (int)Up, to);

                foreach (Square to in push2)
                    *pointer++ = CreateMove(to - (int)(Up + (int)Up), to);

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftSouth() & emptySquares;
                    Bitboard promotionRight = promotionPawns.ShiftSouthWest() & enemies;
                    Bitboard promotionLeft = promotionPawns.ShiftSouthEast() & enemies;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, pointer);

                    foreach (Square to in promotionRight)
                        pointer = GeneratePromotions(to - (int)UpRight, to, pointer);

                    foreach (Square to in promotionLeft)
                        pointer = GeneratePromotions(to - (int)UpLeft, to, pointer);
                }


                //Captures
                Bitboard captureRight = pawns.ShiftSouthWest() & enemies;
                Bitboard captureLeft = pawns.ShiftSouthEast() & enemies;

                foreach (Square to in captureRight)
                    *pointer++ = CreateMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    *pointer++ = CreateMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    Bitboard epCandidate = pawns & WhitePawnAttacks(position.EnPassantSquare);

                    foreach (Square from in epCandidate)
                        *pointer++ = CreateEnPassantMove(from, position.EnPassantSquare);
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

            private static unsafe Span<MoveScore> White(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.White;
                
                Square ksq = position.KingSquare(Us);
                Bitboard occupancy = position.Pieces();

                Bitboard targetSquares = Between(ksq, position.Checkers.LeastSignificantSquare()) | position.Checkers;

                fixed (MoveScore* fix = buffer)
                {
                    MoveScore* ptr = fix;

                    if (!position.Checkers.MoreThanOne())
                    {
                        ptr = GenerateWhitePawnMoves(position, targetSquares, ptr);
                        ptr = GenerateWhiteKnightMoves(position, targetSquares, ptr);
                        ptr = GenerateWhiteBishopMoves(position, targetSquares, occupancy, ptr);
                        ptr = GenerateWhiteRookMoves(position, targetSquares, occupancy, ptr);
                        ptr = GenerateWhiteQueenMoves(position, targetSquares, occupancy, ptr);
                    }

                    //King moves
                    Bitboard kingAttacks = KingAttacks(ksq) & ~position.Pieces(Us);

                    foreach (Square attack in kingAttacks)
                        *ptr++ = CreateMove(ksq, attack);

                    int movesGenerated = (int)(ptr - fix);
                    return buffer[..movesGenerated];
                }
            }

            private static unsafe Span<MoveScore> Black(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.Black;

                Square ksq = position.KingSquare(Us);
                Bitboard occupancy = position.Pieces();

                Bitboard targetSquares = Between(ksq, position.Checkers.LeastSignificantSquare()) | position.Checkers;
                
                fixed (MoveScore* fix = buffer)
                {
                    MoveScore* ptr = fix;

                    if (!position.Checkers.MoreThanOne())
                    {
                        ptr = GenerateBlackPawnMoves(position, targetSquares, ptr);
                        ptr = GenerateBlackKnightMoves(position, targetSquares, ptr);
                        ptr = GenerateBlackBishopMoves(position, targetSquares, occupancy, ptr);
                        ptr = GenerateBlackRookMoves(position, targetSquares, occupancy, ptr);
                        ptr = GenerateBlackQueenMoves(position, targetSquares, occupancy, ptr);
                    }

                    //King moves
                    Bitboard kingAttacks = KingAttacks(ksq) & ~position.Pieces(Us);

                    foreach (Square attack in kingAttacks)
                        *ptr++ = CreateMove(ksq, attack);

                    int movesGenerated = (int)(ptr - fix);
                    return buffer[..movesGenerated];
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe MoveScore* GeneratePromotions(Square from, Square to, MoveScore* pointer)
            {
                *pointer++ = CreatePromotionMove(from, to, PieceType.Queen);
                *pointer++ = CreatePromotionMove(from, to, PieceType.Rook);
                *pointer++ = CreatePromotionMove(from, to, PieceType.Bishop);
                *pointer++ = CreatePromotionMove(from, to, PieceType.Knight);

                return pointer;
            }
           
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe MoveScore* GenerateWhitePawnMoves(Position position, Bitboard targets, MoveScore* pointer)
            {
                const Color Us = Color.White;
                const Color Enemy = Us == Color.White ? Color.Black : Color.White;

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
                    *pointer++ = CreateMove(to - (int)Up, to);

                foreach (Square to in push2)
                    *pointer++ = CreateMove(to - (int)(Up + (int)Up), to);

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftNorth() & emptySquares;                
                    Bitboard promotionRight = promotionPawns.ShiftNorthEast() & enemies;
                    Bitboard promotionLeft = promotionPawns.ShiftNorthWest() & enemies;

                    promotionPush &= targets;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, pointer);

                    foreach (Square to in promotionRight)
                        pointer = GeneratePromotions(to - (int)UpRight, to, pointer);

                    foreach (Square to in promotionLeft)
                        pointer = GeneratePromotions(to - (int)UpLeft, to, pointer);
                }


                //Captures
                Bitboard captureRight = pawns.ShiftNorthEast() & enemies;
                Bitboard captureLeft = pawns.ShiftNorthWest() & enemies;

                foreach (Square to in captureRight)
                    *pointer++ = CreateMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    *pointer++ = CreateMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    if (targets.Contains(position.EnPassantSquare + (int)Up))
                        return pointer;

                    Bitboard epCandidate = pawns & BlackPawnAttacks(position.EnPassantSquare);

                    foreach (Square from in epCandidate)
                        *pointer++ = CreateEnPassantMove(from, position.EnPassantSquare);
                }

                return pointer;
            }
           
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe MoveScore* GenerateBlackPawnMoves(Position position, Bitboard targets, MoveScore* pointer)
            {
                const Color Us = Color.Black;
                const Color Enemy = Us == Color.White ? Color.Black : Color.White;

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
                    *pointer++ = CreateMove(to - (int)Up, to);

                foreach (Square to in push2)
                    *pointer++ = CreateMove(to - (int)(Up + (int)Up), to);

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftSouth() & emptySquares;
                    Bitboard promotionRight = promotionPawns.ShiftSouthWest() & enemies;
                    Bitboard promotionLeft = promotionPawns.ShiftSouthEast() & enemies;

                    promotionPush &= targets;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, pointer);

                    foreach (Square to in promotionRight)
                        pointer = GeneratePromotions(to - (int)UpRight, to, pointer);

                    foreach (Square to in promotionLeft)
                        pointer = GeneratePromotions(to - (int)UpLeft, to, pointer);
                }


                //Captures
                Bitboard captureRight = pawns.ShiftSouthWest() & enemies;
                Bitboard captureLeft = pawns.ShiftSouthEast() & enemies;

                foreach (Square to in captureRight)
                    *pointer++ = CreateMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    *pointer++ = CreateMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    if (targets.Contains(position.EnPassantSquare + (int)Up))
                        return pointer;

                    Bitboard epCandidate = pawns & WhitePawnAttacks(position.EnPassantSquare);

                    foreach (Square from in epCandidate)
                        *pointer++ = CreateEnPassantMove(from, position.EnPassantSquare);
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

            private unsafe static Span<MoveScore> White(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.White;
                const Color Enemy = Us == Color.White ? Color.Black : Color.White;
                Square ksq = position.KingSquare(Us);
                Bitboard occupancy = position.Pieces();
                Bitboard targetSquares = position.Pieces(Enemy);

                fixed (MoveScore* fix = buffer)
                {
                    MoveScore* ptr = fix;

                    ptr = GenerateWhitePawnMoves(position, targetSquares, ptr);
                    ptr = GenerateWhiteKnightMoves(position, targetSquares, ptr);
                    ptr = GenerateWhiteBishopMoves(position, targetSquares, occupancy, ptr);
                    ptr = GenerateWhiteRookMoves(position, targetSquares, occupancy, ptr);
                    ptr = GenerateWhiteQueenMoves(position, targetSquares, occupancy, ptr);

                    //King moves
                    Bitboard kingAttacks = KingAttacks(ksq) & targetSquares;

                    foreach (Square attack in kingAttacks)
                        *ptr++ = CreateMove(ksq, attack);

                    return buffer[..(int)(ptr - fix)];
                }
            }

            private unsafe static Span<MoveScore> Black(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.Black;
                const Color Enemy = Us == Color.White ? Color.Black : Color.White;

                Square ksq = position.KingSquare(Us);
                Bitboard occupancy = position.Pieces();
                Bitboard targetSquares = position.Pieces(Enemy);

                fixed (MoveScore* fix = buffer)
                {
                    MoveScore* ptr = fix;

                    ptr = GenerateBlackPawnMoves(position, targetSquares, ptr);
                    ptr = GenerateBlackKnightMoves(position, targetSquares, ptr);
                    ptr = GenerateBlackBishopMoves(position, targetSquares, occupancy, ptr);
                    ptr = GenerateBlackRookMoves(position, targetSquares, occupancy, ptr);
                    ptr = GenerateBlackQueenMoves(position, targetSquares, occupancy, ptr);

                    //King moves
                    Bitboard kingAttacks = KingAttacks(ksq) & targetSquares;

                    foreach (Square attack in kingAttacks)
                        *ptr++ = CreateMove(ksq, attack);

                    return buffer[..(int)(ptr - fix)];
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe MoveScore* GeneratePromotions(Square from, Square to, MoveScore* pointer)
            {
                *pointer++ = CreatePromotionMove(from, to, PieceType.Queen);

                return pointer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe MoveScore* GenerateCapturePromotions(Square from, Square to, MoveScore* pointer)
            {
                *pointer++ = CreatePromotionMove(from, to, PieceType.Queen);
                *pointer++ = CreatePromotionMove(from, to, PieceType.Rook);
                *pointer++ = CreatePromotionMove(from, to, PieceType.Bishop);
                *pointer++ = CreatePromotionMove(from, to, PieceType.Knight);

                return pointer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe MoveScore* GenerateWhitePawnMoves(Position position, Bitboard targets, MoveScore* pointer)
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

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftNorth() & emptySquares;
                    Bitboard promotionRight = promotionPawns.ShiftNorthEast() & enemies;
                    Bitboard promotionLeft = promotionPawns.ShiftNorthWest() & enemies;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, pointer);

                    foreach (Square to in promotionRight)
                        pointer = GenerateCapturePromotions(to - (int)UpRight, to, pointer);

                    foreach (Square to in promotionLeft)
                        pointer = GenerateCapturePromotions(to - (int)UpLeft, to, pointer);
                }


                //Captures
                Bitboard captureRight = pawns.ShiftNorthEast() & enemies;
                Bitboard captureLeft = pawns.ShiftNorthWest() & enemies;

                foreach (Square to in captureRight)
                    *pointer++ = CreateMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    *pointer++ = CreateMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    Bitboard epCandidates = pawns & BlackPawnAttacks(position.EnPassantSquare);

                    foreach (Square from in epCandidates)
                        *pointer++ = CreateEnPassantMove(from, position.EnPassantSquare);
                }

                return pointer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe MoveScore* GenerateBlackPawnMoves(Position position, Bitboard targets, MoveScore* pointer)
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

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftSouth() & emptySquares;
                    Bitboard promotionRight = promotionPawns.ShiftSouthWest() & enemies;
                    Bitboard promotionLeft = promotionPawns.ShiftSouthEast() & enemies;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, pointer);

                    foreach (Square to in promotionRight)
                        pointer = GenerateCapturePromotions(to - (int)UpRight, to, pointer);

                    foreach (Square to in promotionLeft)
                        pointer = GenerateCapturePromotions(to - (int)UpLeft, to, pointer);
                }


                //Captures
                Bitboard captureRight = pawns.ShiftSouthWest() & enemies;
                Bitboard captureLeft = pawns.ShiftSouthEast() & enemies;

                foreach (Square to in captureRight)
                    *pointer++ = CreateMove(to - (int)UpRight, to);

                foreach (Square to in captureLeft)
                    *pointer++ = CreateMove(to - (int)UpLeft, to);

                if (position.EnPassantSquare != Square.None)
                {
                    Bitboard epCandidate = pawns & WhitePawnAttacks(position.EnPassantSquare);

                    foreach (Square from in epCandidate)
                        *pointer++ = CreateEnPassantMove(from, position.EnPassantSquare);
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

            private static unsafe Span<MoveScore> White(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.White;
                Square ksq = position.KingSquare(Us);
                Bitboard occupancy = position.Pieces();

                Bitboard targetSquares = ~position.Pieces();

                fixed (MoveScore* fix = buffer)
                {
                    MoveScore* ptr = fix;

                    ptr = GenerateWhitePawnMoves(position, targetSquares, ptr);
                    ptr = GenerateWhiteKnightMoves(position, targetSquares, ptr);
                    ptr = GenerateWhiteBishopMoves(position, targetSquares, occupancy, ptr);
                    ptr = GenerateWhiteRookMoves(position, targetSquares, occupancy, ptr);
                    ptr = GenerateWhiteQueenMoves(position, targetSquares, occupancy, ptr);

                    //King moves
                    Bitboard kingAttacks = KingAttacks(ksq) & targetSquares;

                    foreach (Square attack in kingAttacks)
                        *ptr++ = CreateMove(ksq, attack);

                    CastlingRight ourSide = Us == Color.White ? CastlingRight.WhiteSide : CastlingRight.BlackSide;
                    if (position.CanCastle(ourSide))
                    {
                        CastlingRight kingSide = ourSide & CastlingRight.KingSide;
                        if (position.CanCastle(kingSide) && !position.CastlingImpeded(kingSide))
                            *ptr++ = CreateCastlingMove(ksq, position.CastlingRookSquare(kingSide));

                        CastlingRight queenSide = ourSide & CastlingRight.QueenSide;
                        if (position.CanCastle(queenSide) && !position.CastlingImpeded(queenSide))
                            *ptr++ = CreateCastlingMove(ksq, position.CastlingRookSquare(queenSide));
                    }

                    int movesGenerated = (int)(ptr - fix);

                    return buffer[..movesGenerated];
                }
            }

            private static unsafe Span<MoveScore> Black(Position position, Span<MoveScore> buffer)
            {
                const Color Us = Color.Black;
                Square ksq = position.KingSquare(Us);
                Bitboard occupancy = position.Pieces();

                Bitboard targetSquares = ~position.Pieces();

                fixed (MoveScore* fix = buffer)
                {
                    MoveScore* ptr = fix;

                    ptr = GenerateBlackPawnMoves(position, targetSquares, ptr);
                    ptr = GenerateBlackKnightMoves(position, targetSquares, ptr);
                    ptr = GenerateBlackBishopMoves(position, targetSquares, occupancy, ptr);
                    ptr = GenerateBlackRookMoves(position, targetSquares, occupancy, ptr);
                    ptr = GenerateBlackQueenMoves(position, targetSquares, occupancy, ptr);

                    //King moves
                    Bitboard kingAttacks = KingAttacks(ksq) & targetSquares;

                    foreach (Square attack in kingAttacks)
                        *ptr++ = CreateMove(ksq, attack);

                    CastlingRight ourSide = Us == Color.White ? CastlingRight.WhiteSide : CastlingRight.BlackSide;
                    if (position.CanCastle(ourSide))
                    {
                        CastlingRight kingSide = ourSide & CastlingRight.KingSide;
                        if (position.CanCastle(kingSide) && !position.CastlingImpeded(kingSide))
                            *ptr++ = CreateCastlingMove(ksq, position.CastlingRookSquare(kingSide));

                        CastlingRight queenSide = ourSide & CastlingRight.QueenSide;
                        if (position.CanCastle(queenSide) && !position.CastlingImpeded(queenSide))
                            *ptr++ = CreateCastlingMove(ksq, position.CastlingRookSquare(queenSide));
                    }

                    int movesGenerated = (int)(ptr - fix);

                    return buffer[..movesGenerated];
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe MoveScore* GeneratePromotions(Square from, Square to, MoveScore* pointer)
            {
                *pointer++ = CreatePromotionMove(from, to, PieceType.Rook);
                *pointer++ = CreatePromotionMove(from, to, PieceType.Bishop);
                *pointer++ = CreatePromotionMove(from, to, PieceType.Knight);

                return pointer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe MoveScore* GenerateWhitePawnMoves(Position position, Bitboard targets, MoveScore* pointer)
            {
                const Color Us = Color.White;
                const Color Enemy = Us == Color.White ? Color.Black : Color.White;

                const Direction Up = Us == Color.White ? Direction.North : Direction.South;
                const Direction UpRight = Us == Color.White ? Direction.NorthEast : Direction.SouthWest;
                const Direction UpLeft = Us == Color.White ? Direction.NorthWest : Direction.SouthEast;

                const Bitboard RelativeRank7 = Us == Color.White ? Bitboard.Rank7 : Bitboard.Rank2;
                const Bitboard RelativeRank3 = Us == Color.White ? Bitboard.Rank3 : Bitboard.Rank6;

                Bitboard emptySquares = ~position.Pieces();

                Bitboard pawns = position.Pieces(Us, Pawn) & ~RelativeRank7;
                Bitboard promotionPawns = position.Pieces(Us, Pawn) & RelativeRank7;

                //Pushes except promotions
                Bitboard push1 = pawns.ShiftNorth() & emptySquares;
                Bitboard push2 = (push1 & RelativeRank3).ShiftNorth() & emptySquares;

                foreach (Square to in push1)
                    *pointer++ = CreateMove(to - (int)Up, to);

                foreach (Square to in push2)
                    *pointer++ = CreateMove(to - (int)(Up + (int)Up), to);

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftNorth() & emptySquares;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, pointer);
                }

                return pointer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe MoveScore* GenerateBlackPawnMoves(Position position, Bitboard targets, MoveScore* pointer)
            {
                const Color Us = Color.Black;
                const Color Enemy = Us == Color.White ? Color.Black : Color.White;

                const Direction Up = Us == Color.White ? Direction.North : Direction.South;
                const Direction UpRight = Us == Color.White ? Direction.NorthEast : Direction.SouthWest;
                const Direction UpLeft = Us == Color.White ? Direction.NorthWest : Direction.SouthEast;

                const Bitboard RelativeRank7 = Us == Color.White ? Bitboard.Rank7 : Bitboard.Rank2;
                const Bitboard RelativeRank3 = Us == Color.White ? Bitboard.Rank3 : Bitboard.Rank6;

                Bitboard emptySquares = ~position.Pieces();

                Bitboard pawns = position.Pieces(Us, Pawn) & ~RelativeRank7;
                Bitboard promotionPawns = position.Pieces(Us, Pawn) & RelativeRank7;

                //Pushes except promotions
                Bitboard push1 = pawns.ShiftSouth() & emptySquares;
                Bitboard push2 = (push1 & RelativeRank3).ShiftSouth() & emptySquares;

                foreach (Square to in push1)
                    *pointer++ = CreateMove(to - (int)Up, to);

                foreach (Square to in push2)
                    *pointer++ = CreateMove(to - (int)(Up + (int)Up), to);

                //Promotions
                if (promotionPawns != 0)
                {
                    Bitboard promotionPush = promotionPawns.ShiftSouth() & emptySquares;

                    foreach (Square to in promotionPush)
                        pointer = GeneratePromotions(to - (int)Up, to, pointer);
                }
             
                return pointer;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe MoveScore* GenerateWhiteKnightMoves(Position position, Bitboard targetSquares, MoveScore* pointer)
        {
            foreach (Square pieceSquare in position.Pieces(White, Knight)) 
            {
                Bitboard attacks = KnightAttacks(pieceSquare) & targetSquares;

                foreach (Square attackSquare in attacks)
                    *pointer++ = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe MoveScore* GenerateBlackKnightMoves(Position position, Bitboard targetSquares, MoveScore* pointer)
        {
            foreach (Square pieceSquare in position.Pieces(Black, Knight))
            {
                Bitboard attacks = KnightAttacks(pieceSquare) & targetSquares;

                foreach (Square attackSquare in attacks)
                    *pointer++ = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe MoveScore* GenerateWhiteBishopMoves(Position position, Bitboard targetSquares, Bitboard occupiedSquares, MoveScore* pointer)
        {
            foreach (Square pieceSquare in position.Pieces(White, Bishop))
            {
                Bitboard attacks = BishopAttacks(pieceSquare, occupiedSquares) & targetSquares;

                foreach (Square attackSquare in attacks)
                    *pointer++ = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe MoveScore* GenerateBlackBishopMoves(Position position, Bitboard targetSquares, Bitboard occupiedSquares, MoveScore* pointer)
        {
            foreach (Square pieceSquare in position.Pieces(Black, Bishop))
            {
                Bitboard attacks = BishopAttacks(pieceSquare, occupiedSquares) & targetSquares;

                foreach (Square attackSquare in attacks)
                    *pointer++ = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe MoveScore* GenerateWhiteRookMoves(Position position, Bitboard targetSquares, Bitboard occupiedSquares, MoveScore* pointer)
        {
            foreach (Square pieceSquare in position.Pieces(White, Rook))
            {
                Bitboard attacks = RookAttacks(pieceSquare, occupiedSquares) & targetSquares;

                foreach (Square attackSquare in attacks)
                    *pointer++ = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe MoveScore* GenerateBlackRookMoves(Position position, Bitboard targetSquares, Bitboard occupiedSquares, MoveScore* pointer)
        {
            foreach (Square pieceSquare in position.Pieces(Black, Rook))
            {
                Bitboard attacks = RookAttacks(pieceSquare, occupiedSquares) & targetSquares;

                foreach (Square attackSquare in attacks)
                    *pointer++ = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe MoveScore* GenerateWhiteQueenMoves(Position position, Bitboard targetSquares, Bitboard occupiedSquares, MoveScore* pointer)
        {
            foreach (Square pieceSquare in position.Pieces(White, Queen))
            {
                Bitboard attacks = QueenAttacks(pieceSquare, occupiedSquares) & targetSquares;

                foreach (Square attackSquare in attacks)
                    *pointer++ = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe MoveScore* GenerateBlackQueenMoves(Position position, Bitboard targetSquares, Bitboard occupiedSquares, MoveScore* pointer)
        {
            foreach (Square pieceSquare in position.Pieces(Black, Queen))
            {
                Bitboard attacks = QueenAttacks(pieceSquare, occupiedSquares) & targetSquares;

                foreach (Square attackSquare in attacks)
                    *pointer++ = CreateMove(pieceSquare, attackSquare);
            }
            return pointer;
        }
       }
}