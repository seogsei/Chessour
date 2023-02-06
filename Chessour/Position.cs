using Chessour.Evaluation;
using Chessour.MoveGeneration;
using Chessour.Search;
using System.Text;
using static Chessour.Bitboards;
using static Chessour.Color;
using static Chessour.Direction;
using static Chessour.PieceType;

namespace Chessour
{
    internal sealed partial class Position
    {
        private readonly Piece[] board = new Piece[(int)Square.NB];
        private readonly Bitboard[] colorBitboards = new Bitboard[(int)Color.NB];
        private readonly Bitboard[] typeBitboards = new Bitboard[(int)PieceType.NB];
        private readonly int[] pieceCounts = new int[(int)Piece.NB];
        private readonly CastlingRight[] castlingRightMask = new CastlingRight[(int)Square.NB];
        private readonly Square[] castlingRookSquare = new Square[(int)CastlingRight.NB];
        private readonly Bitboard[] castlingPath = new Bitboard[(int)CastlingRight.NB];

        public Position(string fen, StateInfo newSt)
        {
            Set(fen, newSt);

            if (State is null)
                throw new Exception();
        }

        public Color ActiveColor { get; private set; }

        public Score PSQScore { get; private set; }

        public Phase Phase { get; private set; }

        public int GamePly { get; private set; }

        private StateInfo State { get; set; }

        public CastlingRight CastlingRights
        {
            get
            {
                return State.CastlingRights;
            }
        }

        public Square EnPassantSquare
        {
            get
            {
                return State.EnPassantSquare;
            }
        }

        public int FiftyMoveCounter
        {
            get
            {
                return State.HalfMoveClock;
            }
        }

        public Key ZobristKey
        {
            get
            {
                return State.ZobristKey;
            }
        }

        public Bitboard Checkers
        {
            get
            {
                return State.Checkers;
            }
        }

        public Piece CapturedPiece
        {
            get
            {
                return State.Captured;
            }
        }

        public int Repetition
        {
            get
            {
                return State.Repetition;
            }
        }

        public int FullMove
        {
            get
            {
                return (GamePly / 2) + 1;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Piece PieceAt(Square s)
        {
            return board[(int)s];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Square KingSquare(Color c)
        {
            return (colorBitboards[(int)c] & typeBitboards[(int)King]).LeastSignificantSquare();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Square CastlingRookSquare(CastlingRight cr)
        {
            return castlingRookSquare[(int)cr];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard Pieces()
        {
            return typeBitboards[(int)AllPieces];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard Pieces(Color c)
        {
            return colorBitboards[(int)c];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard Pieces(PieceType pt)
        {
            return typeBitboards[(int)pt];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard Pieces(PieceType pt1, PieceType pt2)
        {
            return (typeBitboards[(int)pt1] | typeBitboards[(int)pt2]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard Pieces(Color c, PieceType pt)
        {
            return colorBitboards[(int)c] & typeBitboards[(int)pt];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard Pieces(Color c, PieceType pt1, PieceType pt2)
        {
            return colorBitboards[(int)c] & (typeBitboards[(int)pt1] | typeBitboards[(int)pt2]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard AttackersTo(Square s)
        {
            return AttackersTo(s, Pieces());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard AttackersTo(Square s, Bitboard occupancy)
        {
            return (PawnAttacks(Black, s) & Pieces(White, Pawn))
                | (PawnAttacks(White, s) & Pieces(Black, Pawn))
                | (Attacks(Knight, s) & Pieces(Knight))
                | (BishopAttacks(s, occupancy) & (Pieces(Queen, Bishop)))
                | (RookAttacks(s, occupancy) & (Pieces(Queen, Rook)))
                | (Attacks(King, s) & Pieces(King));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard BlockersForKing(Color c)
        {
            return State.BlockersForKing[(int)c];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard CheckSquares(PieceType pt)
        {
            return State.CheckSquares[(int)pt];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard Pinners(Color side)
        {
            return State.Pinners[(int)side];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int PieceCount(Piece pc)
        {
            return pieceCounts[(int)pc];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCheck()
        {
            return State.Checkers != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanCastle(CastlingRight cr)
        {
            return (CastlingRights & cr) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CastlingImpeded(CastlingRight cr)
        {
            return (Pieces() & castlingPath[(int)cr]) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDraw()
        {
            Debug.Assert(Repetition < 2);
            return FiftyMoveCounter >= 100 || Repetition >= 2; 
        }

        public bool IsPseudoLegal(Move m)
        {
            Color us = ActiveColor;
            Square from = m.From;
            Square to = m.To;
            Piece pc = PieceAt(from);

            if (m.Type != MoveType.Quiet)
                return new MoveList(IsCheck() ? GenerationType.Evasions : GenerationType.NonEvasions,
                                    this,
                                    stackalloc MoveScore[MoveGenerator.MAX_MOVE_COUNT]).Contains(m);

            if ((Pieces(us) & to.ToBitboard()) != 0)
                return false;

            if (pc == Piece.None || pc.ColorOf() != us)
                return false;

            if (m.PromotionPiece != Knight)
                return false;


            if (pc.TypeOf() == Pawn)
            {
                if (((Bitboard.Rank8 | Bitboard.Rank1) & to.ToBitboard()) != 0)
                    return false;

                if (!((PawnAttacks(us, from) & Pieces(us.Flip()) & to.ToBitboard()) != 0)
                    && !(from + (int)PieceConstants.PawnPush(us) == to && PieceAt(to) == Piece.None)
                    && !((from + (int)PieceConstants.PawnPush(us) + (int)PieceConstants.PawnPush(us) == to)
                            && from.GetRank().RelativeTo(us) == Rank.R2
                            && PieceAt(to) == Piece.None
                            && PieceAt(to - (int)PieceConstants.PawnPush(us)) == Piece.None))
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
       
        public bool IsLegal(Move m)
        {
            Color us = ActiveColor;
            Color them = ActiveColor.Flip();

            Square from = m.From;
            Square to = m.To;
            MoveType type = m.Type;

            if (type == MoveType.EnPassant)
            {
                Square ksq = KingSquare(us);
                Square capsq = to - (int)PieceConstants.PawnPush(us);
                Bitboard occupancy = Pieces() ^ from.ToBitboard() ^ capsq.ToBitboard() | to.ToBitboard();

                return (RookAttacks(ksq, occupancy) & Pieces(them, Queen, Rook)) == 0
                    && (BishopAttacks(ksq, occupancy) & Pieces(them, Queen, Bishop)) == 0;
            }

            if (type == MoveType.Castling)
            {
                to = (to > from ? Square.g1 : Square.c1).RelativeTo(us);
                Direction step = to > from ? West : East;

                for (Square s = to; s != from; s += (int)step)
                    if ((AttackersTo(s) & Pieces(them)) != 0)
                        return false;
            }

            if (PieceAt(from).TypeOf() == King)
                return (AttackersTo(to, Pieces() ^ from.ToBitboard()) & Pieces(them)) == 0;

            return (BlockersForKing(us) & from.ToBitboard()) == 0
                || Alligned(from, to, KingSquare(us));
        }
       
        public bool GivesCheck(Move m)
        {
            Square ksq = KingSquare(ActiveColor.Flip());
            Square from = m.From;
            Square to = m.To;

            if ((CheckSquares(PieceAt(from).TypeOf()) & to.ToBitboard()) != 0)
                return true;

            if (((BlockersForKing(ActiveColor.Flip()) & from.ToBitboard()) != 0)
                && !Alligned(from, to, ksq))
                return true;

            switch (m.Type)
            {
                case MoveType.Quiet:
                    return false;
                case MoveType.Promotion:
                    return (Attacks(m.PromotionPiece, to, Pieces() ^ from.ToBitboard()) & ksq.ToBitboard()) != 0;

                case MoveType.EnPassant:
                    Square capSq = MakeSquare(to.GetFile(), from.GetRank());
                    Bitboard b = (Pieces() ^ from.ToBitboard() ^ capSq.ToBitboard()) | to.ToBitboard();

                    return ((RookAttacks(ksq, b) & Pieces(ActiveColor, Queen, Rook)) != 0)
                        | ((BishopAttacks(ksq, b) & Pieces(ActiveColor, Queen, Bishop)) != 0);

                case MoveType.Castling:
                    Square rto = (to > from ? Square.f1 : Square.d1).RelativeTo(ActiveColor);

                    return ((Attacks(Rook, rto) & ksq.ToBitboard()) != 0
                        || (RookAttacks(rto, Pieces() ^ from.ToBitboard() ^ to.ToBitboard()) & ksq.ToBitboard()) != 0);
            }
            return false;
        }

        public bool IsCapture(Move m)
        {
            Debug.Assert(IsLegal(m));

            return (PieceAt(m.To) != Piece.None && m.Type != MoveType.Castling) || m.Type == MoveType.Promotion;
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

        public void Set(Position pos, StateInfo newSt)
        {
            Set(pos.FEN(), newSt);

            State.Previous = pos.State.Previous;
            State.Captured = pos.State.Captured;
            State.Repetition = pos.State.Repetition;
            State.PliesFromNull = pos.State.PliesFromNull;
        }

        public void Set(string fen, StateInfo newSt)
        {
            char token;
            UCIStream stream = new(fen);

            State = newSt;
            Clear();
            State.Clear();

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

                    SetPieceAt(PieceConstants.MakePiece(c, pt), sq++);
                }
            }

            //Active Color
            stream.Extract(out token);
            ActiveColor = token == 'w' ? White : Black;
            stream.Extract(out char _); //Skip whs

            //Castling Rights
            State.CastlingRights = 0;
            void AddCastlingRight(Color c, Square rsq)
            {
                Square ksq = KingSquare(c);
                bool kingSide = rsq > ksq;

                Square kto = (kingSide ? Square.g1 : Square.c1).RelativeTo(c);
                Square rto = (kingSide ? Square.f1 : Square.d1).RelativeTo(c);

                CastlingRight cr = CastlingRightConstants.MakeCastlingRight(c, kingSide ? CastlingRight.KingSide : CastlingRight.QueenSide);

                State.CastlingRights |= cr;
                castlingRightMask[(int)ksq] |= cr;
                castlingRightMask[(int)rsq] |= cr;
                castlingRookSquare[(int)cr] = rsq;
                castlingPath[(int)cr] = (Between(rsq, rto) | Between(ksq, kto) | kto.ToBitboard() | rto.ToBitboard()) & ~(ksq.ToBitboard() | rsq.ToBitboard());
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
            State.EnPassantSquare = Square.None;
            stream.Extract(out token);
            if (token != '-')
            {
                File f = (File)token - 'a';
                stream.Extract(out token);
                Rank r = (Rank)token - '1';

                State.EnPassantSquare = MakeSquare(f, r);
            }
            Debug.Assert(State.EnPassantSquare != (Square)(-97));

            stream.Extract(out char _);

            stream.Extract(out int fiftyMove);
            State.HalfMoveClock = fiftyMove;

            stream.Extract(out int ply);
            GamePly = Math.Max(2 * (ply - 1), 0) + (ActiveColor == White ? 0 : 1);

            SetState(State);


        }

        public void MakeMove(Move m, StateInfo newSt) 
        {
            MakeMove(m, newSt, GivesCheck(m));
        }

        public void MakeMove(Move m, StateInfo newSt, bool givesCheck)
        {
            void HandleCastlingMove(Color us, Square kfrom, Square rfrom, out Square kto, out Square rto)
            {
                kto = (rfrom > kfrom ? Square.g1 : Square.c1).RelativeTo(us);
                rto = (rfrom > kfrom ? Square.f1 : Square.d1).RelativeTo(us);

                Piece king = PieceAt(kfrom);
                Piece rook = PieceAt(rfrom);

                RemovePieceAt(kfrom);
                RemovePieceAt(rfrom);

                SetPieceAt(king, kto);
                SetPieceAt(rook, rto);
            }

            Debug.Assert(State != newSt);

            newSt.Previous = State;

            newSt.ZobristKey = State.ZobristKey;
            newSt.CastlingRights = State.CastlingRights;
            newSt.EnPassantSquare = State.EnPassantSquare;
            newSt.HalfMoveClock = State.HalfMoveClock;
            newSt.PliesFromNull = State.PliesFromNull;

            State = newSt;

            State.HalfMoveClock++;
            State.PliesFromNull++;
            GamePly++;

            Square from = m.From;
            Square to = m.To;
            MoveType type = m.Type;

            Color us = ActiveColor;
            Color them = us.Flip();

            Piece piece = PieceAt(from);
            Piece captured = type == MoveType.EnPassant ? PieceConstants.MakePiece(them, Pawn) : PieceAt(to);

            Debug.Assert(captured.TypeOf() != King);

            if (type == MoveType.Castling)
            {
                Square kfrom = from;
                Square rfrom = to;

                HandleCastlingMove(us, kfrom, rfrom, out Square kto, out Square rto);

                State.ZobristKey ^= Zobrist.PieceKey(piece, kfrom) ^ Zobrist.PieceKey(piece, kto);
                State.ZobristKey ^= Zobrist.PieceKey(captured, rfrom) ^ Zobrist.PieceKey(captured, rto);

                captured = Piece.None;
            }

            if (captured != Piece.None)
            {
                Square capsq = to;

                if (captured.TypeOf() == Pawn)
                    if (type == MoveType.EnPassant)
                        capsq -= (int)PieceConstants.PawnPush(us);

                RemovePieceAt(capsq);
                State.ZobristKey ^= Zobrist.PieceKey(captured, capsq);

                State.HalfMoveClock = 0;
            }

            if (State.EnPassantSquare != Square.None)
            {
                State.EnPassantSquare = Square.None;
                State.ZobristKey ^= Zobrist.EnPassantKey(State.EnPassantSquare);
            }

            if (State.CastlingRights != 0 && (castlingRightMask[(int)from] | castlingRightMask[(int)to]) != 0)
            {
                State.ZobristKey ^= Zobrist.CastlingKey(State.CastlingRights);
                State.CastlingRights &= ~(castlingRightMask[(int)from] | castlingRightMask[(int)to]);
                State.ZobristKey ^= Zobrist.CastlingKey(State.CastlingRights);
            }

            if (type != MoveType.Castling)
            {
                MovePiece(from, to);

                State.ZobristKey ^= Zobrist.PieceKey(piece, from) ^ Zobrist.PieceKey(piece, to);
            }

            if (piece.TypeOf() == Pawn)
            {
                if (((int)from ^ (int)to) == 16)
                {
                    State.EnPassantSquare = to - (int)PieceConstants.PawnPush(us);
                    State.ZobristKey ^= Zobrist.EnPassantKey(State.EnPassantSquare);
                }
                else if (type == MoveType.Promotion)
                {
                    Piece promotionPiece = PieceConstants.MakePiece(us, m.PromotionPiece);

                    RemovePieceAt(to);
                    SetPieceAt(promotionPiece, to);

                    State.ZobristKey ^= Zobrist.PieceKey(piece, to) ^ Zobrist.PieceKey(promotionPiece, to);
                }

                State.HalfMoveClock = 0;
            }

            State.Captured = captured;

            State.Checkers = 0;
            if (givesCheck)
                State.Checkers = AttackersTo(KingSquare(them)) & Pieces(us);

            ActiveColor = them;
            State.ZobristKey ^= Zobrist.SideKey;

            SetCheckInfo(State);

            //Repetition info
            State.Repetition = 0;
            int ply = Math.Min(State.HalfMoveClock, State.PliesFromNull);

            //It requires atleast 4 plies to repeat
            if(ply >= 4)
            {
                StateInfo repeatCand = State.Previous.Previous!;

                for (int i = 4; i < ply; i += 2)
                {
                    repeatCand = repeatCand.Previous!.Previous!;

                    if(State.ZobristKey == repeatCand.ZobristKey)
                    {
                        State.Repetition = repeatCand.Repetition + 1;

                        if (State.Repetition > 1)
                            Console.WriteLine("Three fold repetition");
                        break;
                    }
                }
            }
        }

        public void MakeNullMove(StateInfo newSt)
        {
            newSt.Previous = State;

            newSt.ZobristKey = State.ZobristKey;
            newSt.CastlingRights = State.CastlingRights;
            newSt.EnPassantSquare = State.EnPassantSquare;
            newSt.HalfMoveClock = State.HalfMoveClock;

            State = newSt;

            State.PliesFromNull = 0;
            State.HalfMoveClock++;
            GamePly++;

            if (State.EnPassantSquare != Square.None)
            {
                State.EnPassantSquare = Square.None;
                State.ZobristKey ^= Zobrist.EnPassantKey(State.EnPassantSquare);
            }

            State.Captured = Piece.None;

            ActiveColor = ActiveColor.Flip();
            State.ZobristKey ^= Zobrist.SideKey;

            SetCheckInfo(State);
            State.Checkers = 0;
            State.Repetition = 0;
        }

        public void Takeback(Move move)
        {
            void HandleCastlingMove(Color us, Square kfrom, Square rfrom)
            {
                Square kto = (rfrom > kfrom ? Square.g1 : Square.c1).RelativeTo(us);
                Square rto = (rfrom > kfrom ? Square.f1 : Square.d1).RelativeTo(us);

                Piece king = PieceAt(kto);
                Piece rook = PieceAt(rto);

                RemovePieceAt(kto);
                RemovePieceAt(rto);

                SetPieceAt(king, kfrom);
                SetPieceAt(rook, rfrom);
            }

            ActiveColor = ActiveColor.Flip();

            Square from = move.From;
            Square to = move.To;
            MoveType type = move.Type;

            Color us = ActiveColor;
            Piece captured = State.Captured;

            //Replace the pawn that was promoted
            if (type == MoveType.Promotion)
            {
                RemovePieceAt(to);
                SetPieceAt(PieceConstants.MakePiece(us, Pawn), to);
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
                    capsq -= (int)PieceConstants.PawnPush(us);

                SetPieceAt(captured, capsq);
            }

            if (type == MoveType.Castling)
            {
                HandleCastlingMove(us, from, to);
            }

            //Go back to the previous state object
            State = State.Previous!;

            GamePly--;
        }

        public void TakebackNullMove()
        {
            State = State.Previous!;
            ActiveColor = ActiveColor.Flip();
        }

        private (Bitboard Blockers, Bitboard Pinners) CalculateBlockers(Square target, Color attackers)
        {
            Bitboard blockers = 0;
            Bitboard pinners = 0;
            Bitboard occupancy = Pieces();

            Bitboard snipers = ((Attacks(Rook, target) & Pieces(Queen, Rook))
                               | (Attacks(Bishop, target) & Pieces(Queen, Bishop)))
                                & Pieces(attackers);

            foreach (Square sniperSq in snipers)
            {
                Bitboard betweenPieces = Between(target, sniperSq) & occupancy;

                //There is exactly one piece
                if (betweenPieces != 0 && !betweenPieces.MoreThanOne())
                {
                    //This means there is only one piece between the slider and the target
                    //Which makes the piece a blocker for target
                    blockers |= betweenPieces;

                    //If the blocker is same color as the piece on target square that makes the sniper a pinner
                    //And blocker a pinned piece
                    if ((betweenPieces & Pieces(PieceAt(target).ColorOf())) != 0)
                        pinners |= sniperSq.ToBitboard();
                }
            }

            return (blockers, pinners);
        }

        private void SetCheckInfo(StateInfo si)
        {
            (si.BlockersForKing[(int)White], si.Pinners[(int)Black]) = CalculateBlockers(KingSquare(White), Black);
            (si.BlockersForKing[(int)Black], si.Pinners[(int)White]) = CalculateBlockers(KingSquare(Black), White);

            Square ksq = KingSquare(ActiveColor.Flip());
            si.CheckSquares[(int)Pawn] = PawnAttacks(ActiveColor.Flip(), ksq);
            si.CheckSquares[(int)Knight] = Attacks(Knight, ksq);
            si.CheckSquares[(int)Bishop] = BishopAttacks(ksq, Pieces());
            si.CheckSquares[(int)Rook] = RookAttacks(ksq, Pieces());
            si.CheckSquares[(int)Queen] = si.CheckSquares[(int)Bishop] | si.CheckSquares[(int)Rook];
            si.CheckSquares[(int)King] = 0;
        }

        private void SetState(StateInfo si)
        {
            si.ZobristKey = 0ul;
            si.Checkers = AttackersTo(KingSquare(ActiveColor)) & Pieces(ActiveColor.Flip());

            SetCheckInfo(si);

            foreach (Square sq in Pieces())
            {
                Piece pc = PieceAt(sq);
                si.ZobristKey ^= Zobrist.PieceKey(pc, sq);
            }

            if (ActiveColor == Color.Black)
                si.ZobristKey ^= Zobrist.SideKey;

            if (State.EnPassantSquare != Square.None)
                si.ZobristKey ^= Zobrist.EnPassantKey(State.EnPassantSquare);

            si.ZobristKey ^= Zobrist.CastlingKey(CastlingRights);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetPieceAt(Piece piece, Square square)
        {
            Bitboard bitboard = square.ToBitboard();

            board[(int)square] = piece;
            colorBitboards[(int)piece.ColorOf()] |= bitboard;
            typeBitboards[(int)AllPieces] |= typeBitboards[(int)piece.TypeOf()] |= bitboard; ;
            pieceCounts[(int)piece]++;

            Phase += (int)piece.Phase();
            PSQScore += PSQT.GetScore(piece, square);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemovePieceAt(Square square)
        {
            Piece piece = PieceAt(square);
            Bitboard bitboard = square.ToBitboard();

            board[(int)square] = Piece.None;
            colorBitboards[(int)piece.ColorOf()] ^= bitboard;
            typeBitboards[(int)piece.TypeOf()] ^= bitboard;
            typeBitboards[(int)AllPieces] ^= bitboard;
            pieceCounts[(int)piece]--;

            Phase -= (int)piece.Phase();
            PSQScore -= PSQT.GetScore(piece, square);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MovePiece(Square from, Square to)
        {
            Piece piece = PieceAt(from);
            Bitboard fromto = from.ToBitboard() | to.ToBitboard();

            board[(int)from] = Piece.None;
            board[(int)to] = piece;
            colorBitboards[(int)piece.ColorOf()] ^= fromto;
            typeBitboards[(int)piece.TypeOf()] ^= fromto;
            typeBitboards[(int)AllPieces] ^= fromto;

            PSQScore += PSQT.GetScore(piece, to) - PSQT.GetScore(piece, from);
        }

        private void Clear()
        {
            Array.Clear(board);
            Array.Clear(typeBitboards);
            Array.Clear(colorBitboards);
            Array.Clear(pieceCounts);

            Array.Clear(castlingRookSquare);
            Array.Clear(castlingRightMask);
            Array.Clear(castlingPath);

            ActiveColor = default;
            GamePly = default;
            PSQScore = default;
            Phase = default;
        }

        private static class Cuckoo
        {

        }
    }
}
