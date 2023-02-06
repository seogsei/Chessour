using Chessour.Search;
using static Chessour.Bitboards;
using static Chessour.MoveGeneration.GenerationType;

namespace Chessour.MoveGeneration;

internal enum GenerationType
{
    Captures,
    Quiets,
    NonEvasions,
    Evasions,
}

internal static class MoveGenerator
{
    public const int MAX_MOVE_COUNT = 218;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Generate(Position position, Span<MoveScore> buffer, int start = 0)
    {
        int end = position.IsCheck() ? Generate(Evasions, position, buffer, start)
                                     : Generate(NonEvasions, position, buffer, start);

        Color us = position.ActiveColor;
        Bitboard pinned = position.BlockersForKing(us) & position.Pieces(us);
        Square ksq = position.KingSquare(us);

        for (Move m = buffer[start]; start < end; m = buffer[++start])
        {
            if ((pinned != 0 && (pinned & m.From.ToBitboard()) != 0 || m.From == ksq || m.Type == MoveType.EnPassant)
                && !position.IsLegal(m))
                buffer[start--] = buffer[--end].Move;
            else
                continue;
        }

        return end;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
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
            start = GenerateKnightMoves(us, position, targetSquares, buffer, start);
            start = GenerateBishopMoves(us, position, targetSquares, occupancy, buffer, start);
            start = GenerateRookMoves(us, position, targetSquares, occupancy, buffer, start);
            start = GenerateQueenMoves(us, position, targetSquares, occupancy, buffer, start);
        }

        //King moves
        Bitboard kingAttacks = Attacks(PieceType.King, ksq) & (type == Evasions ? ~position.Pieces(us) : targetSquares);

        foreach (Square attack in kingAttacks)
            buffer[start++] = new Move(ksq, attack);

        if ((type == Quiets || type == NonEvasions) && position.CanCastle(CastlingRightConstants.MakeCastlingRight(us, CastlingRight.All)))
        {
            CastlingRight kingSide = CastlingRightConstants.MakeCastlingRight(us, CastlingRight.KingSide);
            if (position.CanCastle(kingSide) && !position.CastlingImpeded(kingSide))
                buffer[start++] = new Move(ksq, position.CastlingRookSquare(kingSide), MoveType.Castling);

            CastlingRight queenSide = CastlingRightConstants.MakeCastlingRight(us, CastlingRight.QueenSide);
            if (position.CanCastle(queenSide) && !position.CastlingImpeded(queenSide))
                buffer[start++] = new Move(ksq, position.CastlingRookSquare(queenSide), MoveType.Castling);
        }

        return start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GeneratePromotions(GenerationType type, Square from, Square to, Span<MoveScore> buffer, int start)
    {
        if (type == Captures || type == Evasions || type == NonEvasions)
            buffer[start++] = new Move(from, to, PieceType.Queen);

        if (type == Quiets || type == Evasions || type == NonEvasions)
        {
            buffer[start++] = new Move(from, to, PieceType.Rook);
            buffer[start++] = new Move(from, to, PieceType.Bishop);
            buffer[start++] = new Move(from, to, PieceType.Knight);
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
                buffer[start++] = new Move(to - (int)Up, to);

            foreach (Square to in push2)
                buffer[start++] = new Move(to - (int)(Up + (int)Up), to);
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
                start = GeneratePromotions(type, to - (int)Up, to, buffer, start);

            foreach (Square to in captureRight)
                start = GeneratePromotions(type, to - (int)UpRight, to, buffer, start);

            foreach (Square to in captureLeft)
                start = GeneratePromotions(type, to - (int)UpLeft, to, buffer, start);
        }


        //Captures
        if (type == Captures || type == Evasions || type == NonEvasions)
        {
            Bitboard captureRight = pawns.ShiftNorthEast() & enemies;
            Bitboard captureLeft = pawns.ShiftNorthWest() & enemies;

            foreach (Square to in captureRight)
                buffer[start++] = new Move(to - (int)UpRight, to);

            foreach (Square to in captureLeft)
                buffer[start++] = new Move(to - (int)UpLeft, to);

            if (position.EnPassantSquare != Square.None)
            {
                if (type == Evasions && (targets & (position.EnPassantSquare + (int)Up).ToBitboard()) != 0)
                    return start;

                Bitboard epCandidates = pawns & PawnAttacks(enemy, position.EnPassantSquare);

                foreach (Square from in epCandidates)
                    buffer[start++] = new Move(from, position.EnPassantSquare, MoveType.EnPassant);
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
                buffer[start++] = new Move(to - (int)Up, to);

            foreach (Square to in push2)
                buffer[start++] = new Move(to - (int)(Up + (int)Up), to);
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
                start = GeneratePromotions(type, to - (int)Up, to, buffer, start);

            foreach (Square to in captureRight)
                start = GeneratePromotions(type, to - (int)UpRight, to, buffer, start);

            foreach (Square to in captureLeft)
                start = GeneratePromotions(type, to - (int)UpLeft, to, buffer, start);
        }


        //Captures
        if (type == Captures || type == Evasions || type == NonEvasions)
        {
            Bitboard captureRight = pawns.ShiftSouthWest() & enemies;
            Bitboard captureLeft = pawns.ShiftSouthEast() & enemies;

            foreach (Square to in captureRight)
                buffer[start++] = new Move(to - (int)UpRight, to);

            foreach (Square to in captureLeft)
                buffer[start++] = new Move(to - (int)UpLeft, to);

            if (position.EnPassantSquare != Square.None)
            {
                if (type == Evasions && (targets & (position.EnPassantSquare + (int)Up).ToBitboard()) != 0)
                    return start;

                Bitboard epCandidates = pawns & PawnAttacks(enemy, position.EnPassantSquare);

                foreach (Square from in epCandidates)
                    buffer[start++] = new Move(from, position.EnPassantSquare, MoveType.EnPassant);
            }
        }
        return start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GenerateKnightMoves(Color us, Position position, Bitboard targetSquares, Span<MoveScore> buffer, int start)
    {
        foreach (Square pieceSquare in position.Pieces(us, PieceType.Knight))
            foreach (Square attackSquare in Attacks(PieceType.Knight, pieceSquare) & targetSquares)
                buffer[start++] = new Move(pieceSquare, attackSquare);

        return start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GenerateBishopMoves(Color us, Position position, Bitboard targetSquares, Bitboard occupiedSquares, Span<MoveScore> buffer, int start)
    {
        foreach (Square pieceSquare in position.Pieces(us, PieceType.Bishop))
            foreach (Square attackSquare in BishopAttacks(pieceSquare, occupiedSquares) & targetSquares)
                buffer[start++] = new Move(pieceSquare, attackSquare);

        return start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GenerateRookMoves(Color us, Position position, Bitboard targetSquares, Bitboard occupiedSquares, Span<MoveScore> buffer, int start)
    {
        foreach (Square pieceSquare in position.Pieces(us, PieceType.Rook))
            foreach (Square attackSquare in RookAttacks(pieceSquare, occupiedSquares) & targetSquares)
                buffer[start++] = new Move(pieceSquare, attackSquare);

        return start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GenerateQueenMoves(Color us, Position position, Bitboard targetSquares, Bitboard occupiedSquares, Span<MoveScore> buffer, int start)
    {
        foreach (Square pieceSquare in position.Pieces(us, PieceType.Queen))
            foreach (Square attackSquare in QueenAttacks(pieceSquare, occupiedSquares) & targetSquares)
                buffer[start++] = new Move(pieceSquare, attackSquare);

        return start;
    }
}
