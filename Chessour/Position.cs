using Chessour.Evaluation;
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
    public enum Key : ulong
    {

    }

    public sealed class Position
    {
        private readonly Piece[] _board = new Piece[(int)Square.NB];
        private readonly Bitboard[] _colorBitboards = new Bitboard[(int)Color.NB];
        private readonly Bitboard[] _typeBitboards = new Bitboard[(int)PieceType.NB];
        private readonly int[] _pieceCounts = new int[(int)Piece.NB];
        private readonly CastlingRight[] _castlingRightMask = new CastlingRight[(int)Square.NB];
        private readonly Square[] _castlingRookSquare = new Square[(int)CastlingRight.NB];
        private readonly Bitboard[] _castlingPath = new Bitboard[(int)CastlingRight.NB];
        private StateInfo _state;
        private Color _activeColor;
        private int _gamePly;
        private Phase _phase;
        private Score _pSQScore;

        public Position(string fen) : this(fen, new())
        {

        }

        public Position(string fen, StateInfo newSt)
        {
            Set(fen, newSt);

            if (_state is null)
                throw new Exception();
        }

        public Color ActiveColor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _activeColor;
        }

        public Score PSQScore
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pSQScore;
        }

        public Phase Phase
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _phase;
        }

        public int GamePly
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _gamePly;
        }

        public CastlingRight CastlingRights
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _state.castlingRights;
        }

        public Square EnPassantSquare
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _state.epSquare;
        }

        public int FiftyMoveCounter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _state.fiftyMove;
        }

        public Key ZobristKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _state.key;
        }

        public Bitboard Checkers
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _state.checkers;
        }

        public Piece CapturedPiece
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _state.capturedPiece;
        }

        public int Repetition
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _state.repetition;
        }

        public int FullMove
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (GamePly / 2) + 1;
        }


        public Piece PieceAt(Square square)
        {
            return _board[(int)square];
        }

        public Square KingSquare(Color side)
        {
            return (_colorBitboards[(int)side] & _typeBitboards[(int)King]).LeastSignificantSquare();
        }

        public Bitboard Pieces()
        {
            return _typeBitboards[(int)PieceType.AllPieces];
        }

        public Bitboard Pieces(Color color)
        {
            return _colorBitboards[(int)color];
        }

        public Bitboard Pieces(PieceType pieceType)
        {
            return _typeBitboards[(int)pieceType];
        }

        public Bitboard Pieces(PieceType pieceType1, PieceType pieceType2)
        {
            return Pieces(pieceType1) | Pieces(pieceType2);
        }

        public Bitboard Pieces(Color color, PieceType pieceType)
        {
            return _colorBitboards[(int)color] & _typeBitboards[(int)pieceType];
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
            return _state.blockers[(int)color];
        }

        public Bitboard CheckSquares(PieceType pieceType)
        {
            return _state.checkSquares[(int)pieceType];
        }

        public Bitboard Pinners(Color side)
        {
            return _state.pinners[(int)side];
        }

        public int PieceCount(Piece piece)
        {
            return _pieceCounts[(int)piece];
        }

        public bool IsCheck()
        {
            return _state.checkers.IsOccupied();
        }

        public bool CanCastle(CastlingRight castlingRight)
        {
            return (CastlingRights & castlingRight) != 0;
        }

        public bool CastlingImpeded(CastlingRight castlingRight)
        {
            return (Pieces() & _castlingPath[(int)castlingRight]).IsOccupied();
        }

        public Square CastlingRookSquare(CastlingRight castlingRight)
        {
            return _castlingRookSquare[(int)castlingRight];
        }

        public bool IsDraw()
        {
            return FiftyMoveCounter >= 100 || Repetition >= 2;
        }

        public bool IsCapture(Move move)
        {
            Debug.Assert(IsLegal(move));

            return (PieceAt(move.To()) != Piece.None && move.Type() != MoveType.Castling) || move.Type() == MoveType.Promotion;
        }

        public bool IsPseudoLegal(Move move)
        {
            Color us = _activeColor;
            Square from = move.From();
            Square to = move.To();
            Piece pc = PieceAt(from);

            if (move.Type() != MoveType.Quiet)
            {
                foreach (var candMove in MoveGenerator.Generate(IsCheck() ? GenerationType.Evasions : GenerationType.NonEvasions, this, stackalloc MoveScore[256]))
                    if (move == candMove)
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
            Color us = _activeColor;
            Square from = move.From();
            Square to = move.To();

            if (move.Type() == MoveType.EnPassant)
            {
                Square ksq = KingSquare(us);
                Square capsq = to - (int)us.PawnPush();
                Bitboard occupancy = (Pieces() ^ from.ToBitboard() ^ capsq.ToBitboard()) | to.ToBitboard();

                return (RookAttacks(ksq, occupancy) & Pieces(us.Flip(), Queen, Rook)).IsEmpty()
                    && (BishopAttacks(ksq, occupancy) & Pieces(us.Flip(), Queen, Bishop)).IsEmpty();
            }

            if (move.Type() == MoveType.Castling)
            {
                to = (to > from ? Square.g1 : Square.c1).RelativeTo(us);
                Direction step = to > from ? West : East;

                for (Square s = to; s != from; s += (int)step)
                    if ((AttackersTo(s) & Pieces(us.Flip())).IsOccupied())
                        return false;

                return true;
            }

            if (PieceAt(from).TypeOf() == King)
                return (AttackersTo(to, Pieces() ^ from.ToBitboard()) & Pieces(us.Flip())).IsEmpty();

            return (BlockersForKing(us) & from.ToBitboard()).IsEmpty() || Alligned(from, to, KingSquare(us));
        }

        public bool GivesCheck(Move move)
        {
            Color us = _activeColor;
            Square ksq = KingSquare(us.Flip());
            Square from = move.From();
            Square to = move.To();

            if (CheckSquares(PieceAt(from).TypeOf()).Contains(to))
                return true;

            if (BlockersForKing(us.Flip()).Contains(from)
                && !Alligned(from, to, ksq))
                return true;

            if (move.Type() == MoveType.Quiet)
                return false;

            switch (move.Type())
            {
                case MoveType.Promotion:
                    return Attacks(move.PromotionPiece(), to, Pieces() ^ from.ToBitboard()).Contains(ksq);

                case MoveType.Castling:
                    Square rto = (to > from ? Square.f1 : Square.d1).RelativeTo(us);

                    return Attacks(Rook, rto).Contains(ksq)
                        || RookAttacks(rto, Pieces() ^ from.ToBitboard() ^ to.ToBitboard()).Contains(ksq);

                default:
                    Square capSq = MakeSquare(to.GetFile(), from.GetRank());
                    Bitboard b = (Pieces() ^ from.ToBitboard() ^ capSq.ToBitboard()) | to.ToBitboard();

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

            sb.Append(_activeColor == White ? " w " : " b ");

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

        public void Set(Position pos, StateInfo newState)
        {
            Set(pos.FEN(), newState);

            _state.previous = pos._state.previous;
            _state.capturedPiece = pos._state.capturedPiece;
            _state.repetition = pos._state.repetition;
            _state.pliesFromNull = pos._state.pliesFromNull;
        }

        public void Set(string fen, StateInfo newState)
        {
            char token;
            UCIStream stream = new(fen);

            _activeColor = default;
            _gamePly = default;
            _pSQScore = default;
            _phase = default;
            Array.Clear(_board);
            Array.Clear(_typeBitboards);
            Array.Clear(_colorBitboards);
            Array.Clear(_pieceCounts);
            Array.Clear(_castlingRookSquare);
            Array.Clear(_castlingRightMask);
            Array.Clear(_castlingPath);

            _state = newState;
            _state.key = default;
            _state.castlingRights = default;
            _state.epSquare = default;
            _state.fiftyMove = default;
            _state.pliesFromNull = default;
            _state.previous = default;
            _state.capturedPiece = default;
            _state.repetition = default;
            _state.checkers = default;
            Array.Clear(_state.blockers);
            Array.Clear(_state.pinners);
            Array.Clear(_state.checkSquares);

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
            _activeColor = token == 'w' ? White : Black;
            stream.Extract(out char _); //Skip whs

            //Castling Rights
            _state.castlingRights = 0;
            void AddCastlingRight(Color c, Square rsq)
            {
                Square ksq = KingSquare(c);
                bool kingSide = rsq > ksq;

                Square kto = (kingSide ? Square.g1 : Square.c1).RelativeTo(c);
                Square rto = (kingSide ? Square.f1 : Square.d1).RelativeTo(c);

                CastlingRight cr = MakeCastlingRight(c, kingSide ? CastlingRight.KingSide : CastlingRight.QueenSide);

                _state.castlingRights |= cr;
                _castlingRightMask[(int)ksq] |= cr;
                _castlingRightMask[(int)rsq] |= cr;
                _castlingRookSquare[(int)cr] = rsq;
                _castlingPath[(int)cr] = (Between(rsq, rto) | Between(ksq, kto) | kto.ToBitboard() | rto.ToBitboard()) & ~(ksq.ToBitboard() | rsq.ToBitboard());
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
            _state.epSquare = Square.None;
            stream.Extract(out token);
            if (token != '-')
            {
                File f = (File)token - 'a';
                stream.Extract(out token);
                Rank r = (Rank)token - '1';

                _state.epSquare = MakeSquare(f, r);
            }
            Debug.Assert(_state.epSquare != (Square)(-97));

            stream.Extract(out char _);

            stream.Extract(out int fiftyMove);
            _state.fiftyMove = fiftyMove;

            stream.Extract(out int ply);
            _gamePly = Math.Max(2 * (ply - 1), 0) + (_activeColor == White ? 0 : 1);

            SetState(_state);


        }

        public void MakeMove(Move m, StateInfo newSt)
        {
            MakeMove(m, newSt, GivesCheck(m));
        }

        public void MakeMove(Move m, StateInfo newSt, bool givesCheck)
        {
            Debug.Assert(_state != newSt);

            newSt.previous = _state;

            newSt.key = _state.key;
            newSt.castlingRights = _state.castlingRights;
            newSt.epSquare = _state.epSquare;
            newSt.fiftyMove = _state.fiftyMove;
            newSt.pliesFromNull = _state.pliesFromNull;

            _state = newSt;

            _state.fiftyMove++;
            _state.pliesFromNull++;
            _gamePly++;

            Square from = m.From();
            Square to = m.To();
            MoveType type = m.Type();

            Color us = _activeColor;
            Color them = us.Flip();

            Piece piece = PieceAt(from);
            Piece captured = type == MoveType.EnPassant ? MakePiece(them, Pawn) : PieceAt(to);

            Debug.Assert(captured.TypeOf() != King);

            if (type == MoveType.Castling)
            {
                HandleCastlingMove(us, from, to, out Square kingDestination, out Square rookDestination);

                _state.key ^= Zobrist.PieceKey(piece, from) ^ Zobrist.PieceKey(piece, kingDestination);
                _state.key ^= Zobrist.PieceKey(captured, to) ^ Zobrist.PieceKey(captured, rookDestination);

                captured = Piece.None;
            }

            if (captured != Piece.None)
            {
                Square capsq = to;

                if (captured.TypeOf() == Pawn)
                    if (type == MoveType.EnPassant)
                        capsq -= (int)us.PawnPush();

                RemovePieceAt(capsq);
                _state.key ^= Zobrist.PieceKey(captured, capsq);

                _state.fiftyMove = 0;
            }

            if (_state.epSquare != Square.None)
            {
                _state.epSquare = Square.None;
                _state.key ^= Zobrist.EnPassantKey(_state.epSquare);
            }

            if (_state.castlingRights != CastlingRight.None && (_castlingRightMask[(int)from] | _castlingRightMask[(int)to]) != CastlingRight.None)
            {
                _state.key ^= Zobrist.CastlingKey(_state.castlingRights);
                _state.castlingRights &= ~(_castlingRightMask[(int)from] | _castlingRightMask[(int)to]);
                _state.key ^= Zobrist.CastlingKey(_state.castlingRights);
            }

            if (type != MoveType.Castling)
            {
                MovePiece(from, to);

                _state.key ^= Zobrist.PieceKey(piece, from) ^ Zobrist.PieceKey(piece, to);
            }

            if (piece.TypeOf() == Pawn)
            {
                if (((int)from ^ (int)to) == 16)
                {
                    _state.epSquare = to - (int)us.PawnPush();
                    _state.key ^= Zobrist.EnPassantKey(_state.epSquare);
                }
                else if (type == MoveType.Promotion)
                {
                    Piece promotionPiece = MakePiece(us, m.PromotionPiece());

                    RemovePieceAt(to);
                    SetPieceAt(promotionPiece, to);

                    _state.key ^= Zobrist.PieceKey(piece, to) ^ Zobrist.PieceKey(promotionPiece, to);
                }

                _state.fiftyMove = 0;
            }

            _state.capturedPiece = captured;

            _state.checkers = givesCheck ? AttackersTo(KingSquare(them)) & Pieces(us) : Bitboard.Empty;

            _activeColor = them;

            _state.key ^= Zobrist.SideKey;

            SetCheckInfo(_state);

            //Repetition info
            _state.repetition = 0;
            int plies = Math.Min(_state.fiftyMove, _state.pliesFromNull);

            //It requires atleast 4 plies to repeat
            if (plies >= 4)
            {
                StateInfo repeatCandidate = _state.previous.previous!;

                for (int i = 4; i < plies; i += 2)
                {
                    repeatCandidate = repeatCandidate.previous!.previous!;

                    if (_state.key == repeatCandidate.key)
                    {
                        _state.repetition = repeatCandidate.repetition + 1;
                        break;
                    }
                }
            }
        }

        private void HandleCastlingMove(Color us, Square kingFrom, Square rookFrom, out Square kingDestination, out Square rookDestination, bool undo = false)
        {
            bool isKingside = kingFrom < rookFrom;

            kingDestination = (isKingside ? Square.g1 : Square.c1).RelativeTo(us);
            rookDestination = (isKingside ? Square.f1 : Square.d1).RelativeTo(us);

            RemovePieceAt(undo ? kingDestination : kingFrom);
            RemovePieceAt(undo ? rookDestination : rookFrom);
            SetPieceAt(MakePiece(us, King), undo ? kingFrom : kingDestination);
            SetPieceAt(MakePiece(us, Rook), undo ? rookFrom : rookDestination);
        }

        public void MakeNullMove(StateInfo newSt)
        {
            newSt.previous = _state;

            newSt.key = _state.key;
            newSt.castlingRights = _state.castlingRights;
            newSt.epSquare = _state.epSquare;
            newSt.fiftyMove = _state.fiftyMove;

            _state = newSt;

            _state.pliesFromNull = 0;
            _state.fiftyMove++;
            _gamePly++;

            if (_state.epSquare != Square.None)
            {
                _state.epSquare = Square.None;
                _state.key ^= Zobrist.EnPassantKey(_state.epSquare);
            }

            _state.capturedPiece = Piece.None;

            _activeColor = _activeColor.Flip();
            _state.key ^= Zobrist.SideKey;

            SetCheckInfo(_state);
            _state.checkers = 0;
            _state.repetition = 0;
        }

        public void Takeback(Move move)
        {
            _activeColor = _activeColor.Flip();

            Square from = move.From();
            Square to = move.To();
            MoveType type = move.Type();

            Color us = _activeColor;
            Piece captured = _state.capturedPiece;

            //Replace the pawn that was promoted
            if (type == MoveType.Promotion)
            {
                RemovePieceAt(to);
                SetPieceAt(MakePiece(us, Pawn), to);
            }

            if (type != MoveType.Castling)
            {
                //Move the piece back 
                MovePiece(to, from);
            }

            if (captured != Piece.None)
            {
                Square capsq = to;
                if (type == MoveType.EnPassant)
                    capsq -= (int)us.PawnPush();

                SetPieceAt(captured, capsq);
            }

            if (type == MoveType.Castling)
            {
                HandleCastlingMove(us, from, to, out var _, out var _, true);
            }

            //Go back to the previous state object
            _state = _state.previous!;

            _gamePly--;
        }

        public void TakebackNullMove()
        {
            _state = _state.previous!;
            _activeColor = _activeColor.Flip();
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

            Square ksq = KingSquare(_activeColor.Flip());
            state.checkSquares[(int)Pawn] = PawnAttacks(_activeColor.Flip(), ksq);
            state.checkSquares[(int)Knight] = Attacks(Knight, ksq);
            state.checkSquares[(int)Bishop] = BishopAttacks(ksq, Pieces());
            state.checkSquares[(int)Rook] = RookAttacks(ksq, Pieces());
            state.checkSquares[(int)Queen] = state.checkSquares[(int)Bishop] | state.checkSquares[(int)Rook];
            state.checkSquares[(int)King] = Bitboard.Empty;
        }

        private void SetState(StateInfo state)
        {
            state.key = 0ul;
            state.checkers = AttackersTo(KingSquare(_activeColor)) & Pieces(_activeColor.Flip());

            SetCheckInfo(state);

            foreach (Square sq in Pieces())
            {
                Piece pc = PieceAt(sq);
                state.key ^= Zobrist.PieceKey(pc, sq);
            }

            if (_activeColor == Color.Black)
                state.key ^= Zobrist.SideKey;

            if (_state.epSquare != Square.None)
                state.key ^= Zobrist.EnPassantKey(_state.epSquare);

            state.key ^= Zobrist.CastlingKey(CastlingRights);
        }

        private void SetPieceAt(Piece piece, Square square)
        {
            _board[(int)square] = piece;
            _typeBitboards[0] |= square.ToBitboard();
            _typeBitboards[(int)piece.TypeOf()] |= square.ToBitboard();
            _colorBitboards[(int)piece.ColorOf()] |= square.ToBitboard();
            _pieceCounts[(int)piece]++;
            _pSQScore += PSQT.GetScore(piece, square);
        }

        private void RemovePieceAt(Square square)
        {
            Piece piece = _board[(int)square];
            _board[(int)square] = Piece.None;
            _typeBitboards[0] ^= square.ToBitboard();
            _typeBitboards[(int)piece.TypeOf()] ^= square.ToBitboard();
            _colorBitboards[(int)piece.ColorOf()] ^= square.ToBitboard();
            _pieceCounts[(int)piece]--;
            _pSQScore -= PSQT.GetScore(piece, square);
        }

        private void MovePiece(Square from, Square to)
        {
            Bitboard fromto = from.ToBitboard() | to.ToBitboard();
            Piece piece = _board[(int)from];
            _board[(int)from] = Piece.None;
            _board[(int)to] = piece;
            _typeBitboards[0] ^= fromto;
            _typeBitboards[(int)piece.TypeOf()] ^= fromto;
            _colorBitboards[(int)piece.ColorOf()] ^= fromto;
            _pSQScore += PSQT.GetScore(piece, to) - PSQT.GetScore(piece, from);
        }

        private static class Zobrist
        {
            private static readonly Key[,] _piece = new Key[(int)Piece.NB, (int)Square.NB];
            private static readonly Key[] _castling = new Key[(int)CastlingRight.NB];
            private static readonly Key[] _enpassant = new Key[(int)File.NB];
            private static readonly Key _side;

            public static Key SideKey
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _side;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Key PieceKey(Piece piece, Square square)
            {
                return _piece[(int)piece, (int)square];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Key CastlingKey(CastlingRight castlingRights)
            {
                return _castling[(int)castlingRights];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Key EnPassantKey(Square epSquare)
            {
                return _enpassant[(int)epSquare.GetFile()];
            }

            static Zobrist()
            {
                Random rand = new();

                for (Piece p = Piece.None; p < Piece.NB; p++)
                    for (Square s = Square.a1; s < Square.NB; s++)
                        _piece[(int)p, (int)s] = (Key)rand.NextUInt64();

                _side = (Key)rand.NextUInt64();

                for (CastlingRight cr = CastlingRight.None; cr < CastlingRight.NB; cr++)
                    _castling[(int)cr] = (Key)rand.NextUInt64();

                for (File f = File.a; f < File.NB; f++)
                    _enpassant[(int)f] = (Key)rand.NextUInt64();
            }
        }

        public sealed record StateInfo
        {
            internal StateInfo? previous;

            internal Key key;
            internal CastlingRight castlingRights;
            internal Square epSquare;
            internal int fiftyMove;

            internal int pliesFromNull;
            internal int repetition;
            internal Piece capturedPiece;

            internal Bitboard checkers;
            internal readonly Bitboard[] blockers = new Bitboard[(int)Color.NB];
            internal readonly Bitboard[] pinners = new Bitboard[(int)Color.NB];
            internal readonly Bitboard[] checkSquares = new Bitboard[(int)PieceType.NB];

            internal void Clear()
            {
                key = default;
                castlingRights = default;
                epSquare = default;
                fiftyMove = default;
                pliesFromNull = default;
                previous = default;
                capturedPiece = default;
                repetition = default;
                checkers = default;

                Array.Clear(blockers);
                Array.Clear(pinners);
                Array.Clear(checkSquares);
            }
        }
    }
}
