using Chessour.Search;
using Chessour.Utilities;
using System.Text;
using static Chessour.Bitboards;
using static Chessour.BoardRepresentation;
using static Chessour.Color;
using static Chessour.Direction;
using static Chessour.PieceType;

namespace Chessour
{
    public sealed class Position
    {
        public Position(StateInfo stateObject) : this(UCI.StartFEN, stateObject) { }

        public Position(string fen, StateInfo stateObject)
        {
            Set(fen, stateObject);

            if (state is null)
                throw new NullReferenceException();
        }


        //Square centric piece representation
        private readonly Piece[] board = new Piece[(int)Square.NB];

        //Bitboards
        private Bitboard allPieces;
        private readonly Bitboard[] colorBitboards = new Bitboard[(int)Color.NB];
        private readonly Bitboard[] typeBitboards = new Bitboard[(int)PieceType.NB];

        private readonly int[] pieceCounts = new int[(int)Piece.NB];

        //Castling extras
        private readonly CastlingRight[] castlingRightMasks = new CastlingRight[(int)Square.NB];
        private readonly Square[] castlingRookSquares = new Square[(int)CastlingRight.NB];
        private readonly Bitboard[] castlingPaths = new Bitboard[(int)CastlingRight.NB];

        private StateInfo state;

        public Color ActiveColor { get; private set; }
        public ScoreExt PSQScore { get; private set; }
        public Phase Phase { get; private set; }
        public int GamePly { get; private set; }
        public CastlingRight CastlingRights => state.castlingRights;
        public Square EnPassantSquare => state.epSquare;
        public int FiftyMoveCounter => state.fiftyMove;
        public Key PositionKey => state.positionKey;
        public Bitboard Checkers => state.checkers;
        public Piece CapturedPiece => state.capturedPiece;
        public int Repetition => state.repetition;
        public int FullMove => (GamePly / 2) + 1;

        public Piece PieceAt(Square square)
        {
            return board[(int)square];
        }

        public Square KingSquare(Color side)
        {
            return (colorBitboards[(int)side] & typeBitboards[(int)King]).LeastSignificantSquare();
        }

        public Bitboard Pieces()
        {
            return allPieces;
        }

        public Bitboard Pieces(Color color)
        {
            return colorBitboards[(int)color];
        }

        public Bitboard Pieces(PieceType pieceType)
        {
            return typeBitboards[(int)pieceType];
        }

        public Bitboard Pieces(PieceType pieceType1, PieceType pieceType2)
        {
            return Pieces(pieceType1) | Pieces(pieceType2);
        }

        public Bitboard Pieces(Color color, PieceType pieceType)
        {
            return colorBitboards[(int)color] & typeBitboards[(int)pieceType];
        }

        public Bitboard Pieces(Color color, PieceType pieceType1, PieceType pieceType2)
        {
            return Pieces(color) & Pieces(pieceType1, pieceType2);
        }

        public Bitboard AttackersTo(Square square)
        {
            return AttackersTo(square, Pieces());
        }

        public Bitboard AttackersTo(Square square, Bitboard occupancy)
        {
            Bitboard attackers = Bitboard.Empty;
            attackers |= PawnAttacks(White, square) & Pieces(Black, Pawn);
            attackers |= PawnAttacks(Black, square) & Pieces(White, Pawn);
            attackers |= Attacks(Knight, square) & Pieces(Knight);
            attackers |= Attacks(Bishop, square, occupancy) & Pieces(Queen, Bishop);
            attackers |= Attacks(Rook, square, occupancy) & Pieces(Queen, Rook);
            attackers |= Attacks(King, square) & Pieces(King);
            return attackers;
        }

        public Bitboard BlockersForKing(Color color)
        {
            return state.blockers[(int)color];
        }

        public Bitboard CheckSquares(PieceType pieceType)
        {
            return state.checkSquares[(int)pieceType];
        }

        public Bitboard Pinners(Color side)
        {
            return state.pinners[(int)side];
        }

        public int PieceCount(Piece piece)
        {
            return pieceCounts[(int)piece];
        }

        public bool IsCheck()
        {
            return state.checkers.IsOccupied();
        }

        public bool CanCastle(CastlingRight castlingRight)
        {
            return (CastlingRights & castlingRight) != 0;
        }

        public bool CastlingImpeded(CastlingRight castlingRight)
        {
            return (Pieces() & castlingPaths[(int)castlingRight]).IsOccupied();
        }

        public Square CastlingRookSquare(CastlingRight castlingRight)
        {
            return castlingRookSquares[(int)castlingRight];
        }

        public bool IsDraw()
        {
            return FiftyMoveCounter >= 100 || Repetition >= 2;
        }

        public bool IsCapture(Move move)
        {
            Debug.Assert(IsLegal(move));

            return (PieceAt(move.DestinationSquare()) != Piece.None && move.Type() != MoveType.Castling) || move.Type() == MoveType.Promotion;
        }

        public bool IsPseudoLegal(Move move)
        {
            Color us = ActiveColor;
            Square from = move.OriginSquare();
            Square to = move.DestinationSquare();
            Piece pc = PieceAt(from);

            if (move.Type() != MoveType.Quiet)
            {
                var moves = IsCheck() ? MoveGenerator.GenerateEvasions(this, stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT])
                                        : MoveGenerator.GenerateNonEvasions(this, stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT]);

                foreach (var candMove in moves)
                    if (move == candMove.Move)
                        return true;
                return false;
            }

            if ((Pieces(us) & to.ToBitboard()).IsOccupied())
                return false;

            if (pc == Piece.None || pc.ColorOf() != us)
                return false;

            if (move.PromotionPiece() != Knight)
                return false;


            if (pc.TypeOf() == Pawn)
            {
                if ((Bitboard.Rank8 | Bitboard.Rank1).Contains(to))
                    return false;

                if (!((PawnAttacks(us, from) & Pieces(us.Flip()) & to.ToBitboard()) != 0)
                    && !(from + (int)us.PawnPush() == to && PieceAt(to) == Piece.None)
                    && !((from + (int)us.PawnPush() + (int)us.PawnPush() == to)
                            && from.GetRank().RelativeTo(us) == Rank.R2
                            && PieceAt(to) == Piece.None
                            && PieceAt(to - (int)us.PawnPush()) == Piece.None))
                    return false;
            }
            else if (((Attacks(pc.TypeOf(), from, Pieces())) & to.ToBitboard()) == 0)
                return false;


            if (IsCheck())
            {
                if (pc.TypeOf() != King)
                {
                    if (Checkers.MoreThanOne())
                        return false;

                    if (((Between(KingSquare(us), Checkers.LeastSignificantSquare()) | Checkers) & to.ToBitboard()) == 0)
                        return false;
                }
                else if ((AttackersTo(to, Pieces() ^ from.ToBitboard()) & Pieces(us.Flip())) != 0)
                    return false;
            }

            return true;
        }

        public bool IsLegal(Move move)
        {
            Color us = ActiveColor;
            Square origin = move.OriginSquare();
            Square destination = move.DestinationSquare();
            MoveType type = move.Type();

            if (type == MoveType.EnPassant)
            {
                Square ksq = KingSquare(us);
                Square capsq = destination - (int)us.PawnPush();
                Bitboard occupancy = (Pieces() ^ origin.ToBitboard() ^ capsq.ToBitboard()) | destination.ToBitboard();

                return (RookAttacks(ksq, occupancy) & Pieces(us.Flip(), Queen, Rook)).IsEmpty()
                    && (BishopAttacks(ksq, occupancy) & Pieces(us.Flip(), Queen, Bishop)).IsEmpty();
            }
            
            if (type == MoveType.Castling)
            {
                destination = (destination > origin ? Square.g1 : Square.c1).RelativeTo(us);
                Direction step = destination > origin ? West : East;

                for (Square s = destination; s != origin; s += (int)step)
                    if ((AttackersTo(s) & Pieces(us.Flip())).IsOccupied())
                        return false;

                return true;
            }

            if (PieceAt(origin).TypeOf() == King)
                return (AttackersTo(destination, Pieces() ^ origin.ToBitboard()) & Pieces(us.Flip())).IsEmpty();

            return (BlockersForKing(us) & origin.ToBitboard()).IsEmpty() || Alligned(origin, destination, KingSquare(us));
        }

        public bool GivesCheck(Move move)
        {
            Color us = ActiveColor;
            Square ksq = KingSquare(us.Flip());
            Square origin = move.OriginSquare();
            Square destination = move.DestinationSquare();

            if (CheckSquares(PieceAt(origin).TypeOf()).Contains(destination))
                return true;

            if (BlockersForKing(us.Flip()).Contains(origin)
                && !Alligned(origin, destination, ksq))
                return true;

            if (move.Type() == MoveType.Quiet)
                return false;

            switch (move.Type())
            {
                case MoveType.Promotion:
                    return Attacks(move.PromotionPiece(), destination, Pieces() ^ origin.ToBitboard()).Contains(ksq);

                case MoveType.Castling:
                    Square rto = (destination > origin ? Square.f1 : Square.d1).RelativeTo(us);

                    return Attacks(Rook, rto).Contains(ksq)
                        || RookAttacks(rto, Pieces() ^ origin.ToBitboard() ^ destination.ToBitboard()).Contains(ksq);

                default:
                    Square capSq = MakeSquare(destination.GetFile(), origin.GetRank());
                    Bitboard b = (Pieces() ^ origin.ToBitboard() ^ capSq.ToBitboard()) | destination.ToBitboard();

                    return ((RookAttacks(ksq, b) & Pieces(us, Queen, Rook)).IsOccupied())
                        | ((BishopAttacks(ksq, b) & Pieces(us, Queen, Bishop)).IsOccupied());
            }
        }

        public string FEN()
        {
            StringBuilder sb = new();

            for (Rank r = Rank.R8; r >= Rank.R1; r--)
            {
                for (File f = File.a; f <= File.h; f++)
                {
                    int emptyCounter;
                    for (emptyCounter = 0; f <= File.h && PieceAt(MakeSquare(f, r)) == Piece.None; f++)
                        emptyCounter++;
                    if (emptyCounter > 0)
                        sb.Append(emptyCounter);

                    if (f <= File.h)
                        sb.Append(" PNBRQK  pnbrqk"[(int)PieceAt(MakeSquare(f, r))]);
                }
                if (r > Rank.R1)
                    sb.Append('/');
            }

            sb.Append(ActiveColor == White ? " w " : " b ");

            if (CanCastle(CastlingRight.WhiteKingSide))
                sb.Append('K');
            if (CanCastle(CastlingRight.WhiteQueenSide))
                sb.Append('Q');
            if (CanCastle(CastlingRight.BlackKingSide))
                sb.Append('k');
            if (CanCastle(CastlingRight.BlackQueenSide))
                sb.Append('q');
            if (!CanCastle(CastlingRight.All))
                sb.Append('-');

            sb.Append(' ');
            sb.Append(EnPassantSquare != Square.None ? EnPassantSquare : "-");
            sb.Append(' ');
            sb.Append(FiftyMoveCounter);
            sb.Append(' ');
            sb.Append(FullMove);

            return sb.ToString();
        }

        public void Copy(Position pos)
        {
            Array.Copy(pos.board, board, board.Length);
            allPieces = pos.allPieces;
            Array.Copy(pos.colorBitboards, colorBitboards, colorBitboards.Length);
            Array.Copy(pos.typeBitboards, typeBitboards, typeBitboards.Length);
            Array.Copy(pos.pieceCounts, pieceCounts, pieceCounts.Length);
            Array.Copy(pos.castlingRightMasks, castlingRightMasks, castlingRightMasks.Length);
            Array.Copy(pos.castlingRookSquares, castlingRookSquares, castlingRookSquares.Length);
            Array.Copy(pos.castlingPaths, castlingPaths, castlingPaths.Length);
            state = pos.state;
            ActiveColor = pos.ActiveColor;
            GamePly = pos.GamePly;
            Phase = pos.Phase;
            PSQScore = pos.PSQScore;
        }

        public void Set(string fen, StateInfo newState)
        {
            char token;
            UCIStream stream = new(fen);

            ActiveColor = default;
            GamePly = default;
            PSQScore = default;
            Phase = default;
            Array.Clear(board);
            allPieces = default;
            Array.Clear(typeBitboards);
            Array.Clear(colorBitboards);
            Array.Clear(pieceCounts);
            Array.Clear(castlingRookSquares);
            Array.Clear(castlingRightMasks);
            Array.Clear(castlingPaths);

            state = newState;
            state.positionKey = default;
            state.castlingRights = default;
            state.epSquare = default;
            state.fiftyMove = default;
            state.pliesFromNull = default;
            state.previous = default;
            state.capturedPiece = default;
            state.repetition = default;
            state.checkers = default;
            Array.Clear(state.blockers);
            Array.Clear(state.pinners);
            Array.Clear(state.checkSquares);

            //Piece Placement
            Square sq = Square.a8;
            while (stream.Extract(out token) && token != ' ')
            {
                if (char.IsDigit(token))
                {
                    sq += token - '0';
                }
                else if (token == '/')
                {
                    sq += (int)South * 2;
                }
                else
                {
                    Color c = char.IsUpper(token) ? White : Black;
                    PieceType pt = (PieceType)" pnbrqk".IndexOf(char.ToLower(token));

                    SetPieceAt(MakePiece(c, pt), sq++);
                }
            }

            //Active Color
            stream.Extract(out token);
            ActiveColor = token == 'w' ? White : Black;
            stream.Extract(out char _); //Skip whs

            //Castling Rights
            state.castlingRights = 0;
            void AddCastlingRight(Color c, Square rsq)
            {
                Square ksq = KingSquare(c);
                bool kingSide = rsq > ksq;

                Square kto = (kingSide ? Square.g1 : Square.c1).RelativeTo(c);
                Square rto = (kingSide ? Square.f1 : Square.d1).RelativeTo(c);

                CastlingRight cr = MakeCastlingRight(c, kingSide ? CastlingRight.KingSide : CastlingRight.QueenSide);

                state.castlingRights |= cr;
                castlingRightMasks[(int)ksq] |= cr;
                castlingRightMasks[(int)rsq] |= cr;
                castlingRookSquares[(int)cr] = rsq;
                castlingPaths[(int)cr] = (Between(rsq, rto) | Between(ksq, kto) | kto.ToBitboard() | rto.ToBitboard()) & ~(ksq.ToBitboard() | rsq.ToBitboard());
            }

            while (stream.Extract(out token) && token != ' ')
            {
                if (token == '-')
                {
                    stream.Extract(out char _);
                    break;
                }

                Color c = char.IsUpper(token) ? White : Black;
                Square rsq = (char.ToLower(token) == 'k' ? Square.h1 : Square.a1).RelativeTo(c);

                AddCastlingRight(c, rsq);
            }

            //En passant square
            state.epSquare = Square.None;
            stream.Extract(out token);
            if (token != '-')
            {
                File f = (File)token - 'a';
                stream.Extract(out token);
                Rank r = (Rank)token - '1';

                state.epSquare = MakeSquare(f, r);
            }
            Debug.Assert(state.epSquare != (Square)(-97));

            stream.Extract(out char _);

            stream.Extract(out int fiftyMove);
            state.fiftyMove = fiftyMove;

            stream.Extract(out int ply);
            GamePly = Math.Max(2 * (ply - 1), 0) + (ActiveColor == White ? 0 : 1);

            SetState(state);
        }

        public void MakeMove(Move move, StateInfo newSt)
        {
            MakeMove(move, newSt, GivesCheck(move));
        }

        public void MakeMove(Move move, StateInfo newState, bool givesCheck)
        {
            void MakeCastlingMove(Color us, Square kingOrigin, Square rookOrigin, out Square kingDestination, out Square rookDestination)
            {
                bool isKingside = kingOrigin < rookOrigin;

                kingDestination = (isKingside ? Square.g1 : Square.c1).RelativeTo(us);
                rookDestination = (isKingside ? Square.f1 : Square.d1).RelativeTo(us);

                RemovePieceAt(kingOrigin);
                RemovePieceAt(rookOrigin);
                SetPieceAt(MakePiece(us, King), kingDestination);
                SetPieceAt(MakePiece(us, Rook), rookDestination);
            }

            Debug.Assert(state != newState);

            newState.previous = state;

            newState.positionKey = state.positionKey;
            newState.castlingRights = state.castlingRights;
            newState.epSquare = state.epSquare;
            newState.fiftyMove = state.fiftyMove;
            newState.pliesFromNull = state.pliesFromNull;

            state = newState;

            state.fiftyMove++;
            state.pliesFromNull++;
            GamePly++;

            Square origin = move.OriginSquare();
            Square destination = move.DestinationSquare();
            MoveType type = move.Type();

            Color us = ActiveColor;
            Color them = us.Flip();

            Piece piece = PieceAt(origin);
            Piece captured = type == MoveType.EnPassant ? MakePiece(them, Pawn) : PieceAt(destination);

            Debug.Assert(captured.TypeOf() != King);

            if (type == MoveType.Castling)
            {
                MakeCastlingMove(us, origin, destination, out Square kingDestination, out Square rookDestination);

                state.positionKey ^= Zobrist.PieceKey(piece, origin) ^ Zobrist.PieceKey(piece, kingDestination);
                state.positionKey ^= Zobrist.PieceKey(captured, destination) ^ Zobrist.PieceKey(captured, rookDestination);

                captured = Piece.None;
            }

            if (captured != Piece.None)
            {
                Square capsq = destination;

                if (captured.TypeOf() == Pawn)
                    if (type == MoveType.EnPassant)
                        capsq -= (int)us.PawnPush();

                RemovePieceAt(capsq);
                state.positionKey ^= Zobrist.PieceKey(captured, capsq);

                state.fiftyMove = 0;
            }

            if (state.epSquare != Square.None)
            {
                state.epSquare = Square.None;
                state.positionKey ^= Zobrist.EnPassantKey(state.epSquare);
            }

            if (state.castlingRights != CastlingRight.None && (castlingRightMasks[(int)origin] | castlingRightMasks[(int)destination]) != CastlingRight.None)
            {
                state.positionKey ^= Zobrist.CastlingKey(state.castlingRights);
                state.castlingRights &= ~(castlingRightMasks[(int)origin] | castlingRightMasks[(int)destination]);
                state.positionKey ^= Zobrist.CastlingKey(state.castlingRights);
            }

            if (type != MoveType.Castling)
            {
                MovePiece(origin, destination);
                state.positionKey ^= Zobrist.PieceKey(piece, origin) ^ Zobrist.PieceKey(piece, destination);
            }

            if (piece.TypeOf() == Pawn)
            {
                if (((int)origin ^ (int)destination) == 16)
                {
                    state.epSquare = destination - (int)us.PawnPush();
                    state.positionKey ^= Zobrist.EnPassantKey(state.epSquare);
                }
                else if (type == MoveType.Promotion)
                {
                    Piece promotionPiece = MakePiece(us, move.PromotionPiece());

                    RemovePieceAt(destination);
                    SetPieceAt(promotionPiece, destination);
                    state.positionKey ^= Zobrist.PieceKey(piece, destination) ^ Zobrist.PieceKey(promotionPiece, destination);
                }

                state.fiftyMove = 0;
            }

            state.capturedPiece = captured;

            state.checkers = givesCheck ? AttackersTo(KingSquare(them)) & Pieces(us) : Bitboard.Empty;

            ActiveColor = them;

            state.positionKey ^= Zobrist.SideKey();

            SetCheckInfo(state);

            //Repetition info
            state.repetition = 0;
            int plies = Math.Min(state.fiftyMove, state.pliesFromNull);

            //It requires atleast 4 plies to repeat
            if (plies >= 4)
            {
                StateInfo repeatCandidate = state.previous.previous!;

                for (int i = 4; i < plies; i += 2)
                {
                    repeatCandidate = repeatCandidate.previous!.previous!;

                    if (state.positionKey == repeatCandidate.positionKey)
                    {
                        state.repetition = repeatCandidate.repetition + 1;
                        break;
                    }
                }
            }
        }

        public void MakeNullMove(StateInfo newSt)
        {
            newSt.previous = state;

            newSt.positionKey = state.positionKey;
            newSt.castlingRights = state.castlingRights;
            newSt.epSquare = state.epSquare;
            newSt.fiftyMove = state.fiftyMove;

            state = newSt;

            state.pliesFromNull = 0;
            state.fiftyMove++;
            GamePly++;

            if (state.epSquare != Square.None)
            {
                state.epSquare = Square.None;
                state.positionKey ^= Zobrist.EnPassantKey(state.epSquare);
            }

            state.capturedPiece = Piece.None;

            ActiveColor = ActiveColor.Flip();
            state.positionKey ^= Zobrist.SideKey();

            SetCheckInfo(state);
            state.checkers = 0;
            state.repetition = 0;
        }

        public void Takeback(Move move)
        {
            void MakeCastlingMove(Color us, Square kingOrigin, Square rookOrigin, out Square kingDestination, out Square rookDestination)
            {
                bool isKingside = kingOrigin < rookOrigin;

                kingDestination = (isKingside ? Square.g1 : Square.c1).RelativeTo(us);
                rookDestination = (isKingside ? Square.f1 : Square.d1).RelativeTo(us);

                RemovePieceAt(kingDestination);
                RemovePieceAt(rookDestination);
                SetPieceAt(MakePiece(us, King), kingOrigin);
                SetPieceAt(MakePiece(us, Rook), rookOrigin);
            }

            ActiveColor = ActiveColor.Flip();

            Square origin = move.OriginSquare();
            Square destination = move.DestinationSquare();
            MoveType type = move.Type();

            Color us = ActiveColor;
            Piece captured = state.capturedPiece;

            //Replace the pawn that was promoted
            if (type == MoveType.Promotion)
            {
                RemovePieceAt(destination);
                SetPieceAt(MakePiece(us, Pawn), destination);
            }

            if (type != MoveType.Castling)
            {
                //Move the piece back 
                MovePiece(destination, origin);
            }

            if (captured != Piece.None)
            {
                Square capsq = destination;
                if (type == MoveType.EnPassant)
                    capsq -= (int)us.PawnPush();

                SetPieceAt(captured, capsq);
            }

            if (type == MoveType.Castling)
            {
                MakeCastlingMove(us, origin, destination, out var _, out var _);
            }

            //Go back to the previous state object
            state = state.previous!;

            GamePly--;
        }

        public void TakebackNullMove()
        {
            state = state.previous!;
            ActiveColor = ActiveColor.Flip();
            GamePly--;
        }

        private (Bitboard Blockers, Bitboard Pinners) CalculateBlockers(Square target, Color attackers)
        {
            Bitboard blockers = Bitboard.Empty;
            Bitboard pinners = Bitboard.Empty;
            Bitboard occupancy = Pieces();

            Bitboard snipers = ((Attacks(Rook, target) & Pieces(Queen, Rook)) | (Attacks(Bishop, target) & Pieces(Queen, Bishop)))
                                & Pieces(attackers);

            foreach (Square sniper in snipers)
            {
                Bitboard betweenPieces = Between(target, sniper) & occupancy;

                //There is exactly one piece
                if (betweenPieces.IsOccupied() && !betweenPieces.MoreThanOne())
                {
                    //This means there is only one piece between the slider and the target
                    //Which makes the piece a blocker for target
                    blockers |= betweenPieces;

                    //If the blocker is same color as the piece on target square that makes the sniper a pinner
                    //And blocker a pinned piece
                    if ((betweenPieces & Pieces(PieceAt(target).ColorOf())).IsOccupied())
                        pinners |= sniper.ToBitboard();
                }
            }

            return (blockers, pinners);
        }

        private void SetCheckInfo(StateInfo state)
        {
            (state.blockers[(int)White], state.pinners[(int)Black]) = CalculateBlockers(KingSquare(White), Black);
            (state.blockers[(int)Black], state.pinners[(int)White]) = CalculateBlockers(KingSquare(Black), White);

            Square ksq = KingSquare(ActiveColor.Flip());
            state.checkSquares[(int)Pawn] = PawnAttacks(ActiveColor.Flip(), ksq);
            state.checkSquares[(int)Knight] = Attacks(Knight, ksq);
            state.checkSquares[(int)Bishop] = BishopAttacks(ksq, Pieces());
            state.checkSquares[(int)Rook] = RookAttacks(ksq, Pieces());
            state.checkSquares[(int)Queen] = state.checkSquares[(int)Bishop] | state.checkSquares[(int)Rook];
            state.checkSquares[(int)King] = Bitboard.Empty;
        }

        private void SetState(StateInfo state)
        {
            state.positionKey = 0ul;
            state.checkers = AttackersTo(KingSquare(ActiveColor)) & Pieces(ActiveColor.Flip());

            SetCheckInfo(state);

            foreach (Square sq in Pieces())
            {
                Piece pc = PieceAt(sq);
                state.positionKey ^= Zobrist.PieceKey(pc, sq);
            }

            if (ActiveColor == Black)
                state.positionKey ^= Zobrist.SideKey();

            if (this.state.epSquare != Square.None)
                state.positionKey ^= Zobrist.EnPassantKey(this.state.epSquare);

            state.positionKey ^= Zobrist.CastlingKey(CastlingRights);
        }

        private void SetPieceAt(Piece piece, Square square)
        {
            Bitboard squareBB = square.ToBitboard();

            board[(int)square] = piece;
            allPieces |= squareBB;
            typeBitboards[(int)piece.TypeOf()] |= squareBB;
            colorBitboards[(int)piece.ColorOf()] |= squareBB;
            pieceCounts[(int)piece]++;

            PSQScore += Evaluation.PSQT.Get(piece, square);
            Phase += (int)piece.PhaseValue();
        }

        private void RemovePieceAt(Square square)
        {
            Piece piece = board[(int)square];
            Bitboard squareBB = square.ToBitboard();

            board[(int)square] = Piece.None;
            allPieces ^= squareBB;
            typeBitboards[(int)piece.TypeOf()] ^= squareBB;
            colorBitboards[(int)piece.ColorOf()] ^= squareBB;
            pieceCounts[(int)piece]--;

            PSQScore -= Evaluation.PSQT.Get(piece, square);
            Phase -= piece.PhaseValue();
        }

        private void MovePiece(Square from, Square to)
        {
            Bitboard fromto = from.ToBitboard() | to.ToBitboard();
            Piece piece = board[(int)from];

            board[(int)from] = Piece.None;
            board[(int)to] = piece;
            allPieces ^= fromto;
            typeBitboards[(int)piece.TypeOf()] ^= fromto;
            colorBitboards[(int)piece.ColorOf()] ^= fromto;

            PSQScore += Evaluation.PSQT.Get(piece, to) - Evaluation.PSQT.Get(piece, from);
        }

        public override string ToString()
        {
            StringBuilder sb = new(1024);

            sb.AppendLine(" +---+---+---+---+---+---+---+---+");

            for(Rank r = Rank.R8; r >= Rank.R1; r--) 
            {
                for (File f = File.a; f <= File.h; f++)
                    sb.Append(" | ").Append(PieceToChar(PieceAt(MakeSquare(f, r))));

                sb.Append(" | ").Append((int)(r + 1)).AppendLine();
                sb.AppendLine(" +---+---+---+---+---+---+---+---+");
            }

            sb.AppendLine("   a   b   c   d   e   f   g   h");
            sb.AppendLine();

            sb.Append("Fen: ").Append(FEN()).AppendLine();
            sb.Append("Key: ").Append(PositionKey.ToString("X")).AppendLine();
            return sb.ToString();
        }

        private static class Zobrist
        {
            private static readonly Key[,] pieceKeys = new Key[(int)Piece.NB, (int)Square.NB];
            private static readonly Key[] castlingKeys = new Key[(int)CastlingRight.NB];
            private static readonly Key[] enpassantKeys = new Key[(int)File.NB];
            private static readonly Key sideKey;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Key SideKey()
            {
                return sideKey;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Key PieceKey(Piece piece, Square square)
            {
                return pieceKeys[(int)piece, (int)square];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Key CastlingKey(CastlingRight castlingRights)
            {
                return castlingKeys[(int)castlingRights];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Key EnPassantKey(Square epSquare)
            {
                return enpassantKeys[(int)epSquare.GetFile()];
            }

            static Zobrist()
            {
                XORSHift64 prng = new(4211);

                for (Piece p = Piece.None; p < Piece.NB; p++)
                    for (Square s = Square.a1; s <= Square.h8; s++)
                        pieceKeys[(int)p, (int)s] = (Key)prng.NextUInt64();

                sideKey = (Key)prng.NextUInt64();

                for (CastlingRight cr = CastlingRight.None; cr < CastlingRight.NB; cr++)
                    castlingKeys[(int)cr] = (Key)prng.NextUInt64();

                for (File f = File.a; f <= File.h; f++)
                    enpassantKeys[(int)f] = (Key)prng.NextUInt64();
            }
        }

        public sealed record StateInfo
        {
            internal StateInfo? previous;

            internal int fiftyMove;
            internal int pliesFromNull;
            internal int repetition;

            internal CastlingRight castlingRights;
            internal Square epSquare;
            internal Piece capturedPiece;

            internal Bitboard checkers;
            internal Key positionKey;

            internal readonly Bitboard[] blockers = new Bitboard[(int)Color.NB];
            internal readonly Bitboard[] pinners = new Bitboard[(int)Color.NB];
            internal readonly Bitboard[] checkSquares = new Bitboard[(int)PieceType.NB];
        }
    }
}
