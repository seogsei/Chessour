using System.Text;
using static Chessour.Bitboards;
using static Chessour.Types.Color;
using static Chessour.Types.Direction;
using static Chessour.Types.PieceType;

namespace Chessour
{
    sealed class Position
    {
        readonly Piece[] board = new Piece[(int)Square.NB];
        readonly Bitboard[] colorBitboards = new Bitboard[(int)Color.NB];
        readonly Bitboard[] typeBitboards = new Bitboard[(int)PieceType.NB];
        readonly int[] pieceCounts = new int[(int)Piece.NB];
        readonly CastlingRight[] castlingRightMask = new CastlingRight[(int)Square.NB];
        readonly Square[] castlingRookSquare = new Square[(int)CastlingRight.NB];
        readonly Bitboard[] castlingPath = new Bitboard[(int)CastlingRight.NB];
        StateInfo state;
        int gamePly;

        public Position(string fen, StateInfo newSt)
        {
            Set(fen, newSt);

            if (state is null)
                throw new Exception();
        }

        public Color ActiveColor { get; private set; }

        public Score PSQScore { get; private set; }

        public CastlingRight CastlingRights
        {
            get
            {
                return state.CastlingRights;
            }
        }

        public Square EnPassantSquare
        {
            get
            {
                return state.EnPassantSquare;
            }
        }

        public int FiftyMoveCounter
        {
            get
            {
                return state.FiftyMoveCounter;
            }
        }

        public Key ZobristKey
        {
            get
            {
                return state.ZobristKey;
            }
        }

        public Bitboard Checkers
        {
            get
            {
                return state.Checkers;
            }
        }

        public Piece CapturedPiece
        {
            get
            {
                return state.CapturedPiece;
            }
        }

        public int FullMove
        {
            get
            {
                return (gamePly / 2) + 1;
            }
        }

        public Piece PieceAt(Square s)
        {
            return board[(int)s];
        }
        
        public Square KingSquare(Color c)
        {
            return (colorBitboards[(int)c] & typeBitboards[(int)King]).LeastSignificantSquare();
        }
     
        public Square CastlingRookSquare(CastlingRight cr)
        {
            return castlingRookSquare[(int)cr];
        }

        public Bitboard Pieces()
        {
            return typeBitboards[(int)AllPieces];
        }
       
        public Bitboard Pieces(Color c)
        {
            return colorBitboards[(int)c];
        }
      
        public Bitboard Pieces(PieceType pt)
        {
            return typeBitboards[(int)pt];
        }
       
        public Bitboard Pieces(PieceType pt1, PieceType pt2)
        {
            return (typeBitboards[(int)pt1] | typeBitboards[(int)pt2]);
        }
      
        public Bitboard Pieces(Color c, PieceType pt)
        {
            return colorBitboards[(int)c] & typeBitboards[(int)pt];
        }
      
        public Bitboard Pieces(Color c, PieceType pt1, PieceType pt2)
        {
            return colorBitboards[(int)c] & (typeBitboards[(int)pt1] | typeBitboards[(int)pt2]);
        }
      
        public Bitboard AttackersTo(Square s)
        {
            return AttackersTo(s, Pieces());
        }
       
        public Bitboard AttackersTo(Square s, Bitboard occupancy)
        {
            return (PawnAttacks(Black, s) & Pieces(White, Pawn))
                | (PawnAttacks(White, s) & Pieces(Black, Pawn))
                | (Attacks(Knight, s) & Pieces(Knight))
                | (Attacks(Bishop, s, occupancy) & (Pieces(Queen, Bishop)))
                | (Attacks(Rook, s, occupancy) & (Pieces(Queen, Rook)))
                | (Attacks(King, s) & Pieces(King));
        }
       
        public Bitboard BlockersForKing(Color c)
        {
            return state.BlockersForKing[(int)c];
        }
       
        public Bitboard CheckSquares(PieceType pt)
        {
            return state.CheckSquares[(int)pt];
        }
       
        public Bitboard Pinners(Color side)
        {
            return state.Pinners[(int)side];
        }
       
        public int PieceCount(Piece pc)
        {
            return pieceCounts[(int)pc];
        }
      
        public bool IsCheck()
        {
            return state.Checkers != 0;
        }
       
        public bool IsPseudoLegal(Move m)
        {
            Color us = ActiveColor;
            Square from = m.FromSquare();
            Square to = m.ToSquare();
            Piece pc = PieceAt(from);

            if (m.TypeOf() != MoveType.Quiet)
                return new MoveList(IsCheck() ? GenerationType.Evasions : GenerationType.NonEvasions,
                                    this,
                                    stackalloc MoveScore[MAX_MOVE_COUNT]).Contains(m);

            if ((Pieces(us) & to.ToBitboard()) != 0)
                return false;

            if (pc == Piece.None || pc.GetColor() != us)
                return false;

            if (m.PromotionPiece() != Knight)
                return false;


            if (Core.GetPieceType(pc) == Pawn)
            {
                if (((Bitboard.Rank8 | Bitboard.Rank1) & to.ToBitboard()) != 0)
                    return false;

                if (!((PawnAttacks(us, from) & Pieces(us.Flip()) & to.ToBitboard()) != 0)
                    && !(from.Shift(PawnPush(us)) == to && PieceAt(to) == Piece.None)
                    && !((from.Shift(PawnPush(us)).Shift(PawnPush(us)) == to)
                            && from.GetRank().RelativeTo(us) == Rank.R2
                            && PieceAt(to) == Piece.None
                            && PieceAt(to.NegativeShift(PawnPush(us))) == Piece.None))
                    return false;
            }
            else if (((Attacks(Core.GetPieceType(pc), from, Pieces())) & to.ToBitboard()) == 0)
                return false;


            if (IsCheck())
            {
                if (Core.GetPieceType(pc) != King)
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

            Square from = m.FromSquare();
            Square to = m.ToSquare();
            MoveType type = m.TypeOf();

            if (type == MoveType.EnPassant)
            {
                Square ksq = KingSquare(us);
                Square capsq = to.NegativeShift(PawnPush(us));
                Bitboard occupancy = Pieces() ^ from.ToBitboard() ^ capsq.ToBitboard() | to.ToBitboard();

                return (Attacks(Rook, ksq, occupancy) & Pieces(them, Queen, Rook)) == 0
                    && (Attacks(Bishop, ksq, occupancy) & Pieces(them, Queen, Bishop)) == 0;
            }

            if (type == MoveType.Castling)
            {
                to = (to > from ? Square.g1 : Square.c1).RelativeTo(us);
                Direction step = to > from ? West : East;

                for (Square s = to; s != from; s = s.Shift(step))
                    if ((AttackersTo(s) & Pieces(them)) != 0)
                        return false;
            }

            if (Core.GetPieceType(PieceAt(from)) == King)
                return (AttackersTo(to, Pieces() ^ from.ToBitboard()) & Pieces(them)) == 0;

            return (BlockersForKing(us) & from.ToBitboard()) == 0
                || Alligned(from, to, KingSquare(us));
        }
       
        public bool GivesCheck(Move m)
        {
            Square from = m.FromSquare();
            Square to = m.ToSquare();

            if ((CheckSquares(Core.GetPieceType(PieceAt(from))) & to.ToBitboard()) != 0)
                return true;

            if (((BlockersForKing(ActiveColor.Flip()) & from.ToBitboard()) != 0)
                && !Alligned(from, to, KingSquare(ActiveColor.Flip())))
                return true;

            switch (m.TypeOf())
            {
                case MoveType.Quiet:
                    return false;
                case MoveType.Promotion:
                    return (Attacks(m.PromotionPiece(), to, Pieces() ^ from.ToBitboard()) & KingSquare(ActiveColor.Flip()).ToBitboard()) != 0;

                case MoveType.EnPassant:
                    Square capSq = MakeSquare(to.GetFile(), from.GetRank());
                    Bitboard b = (Pieces() ^ from.ToBitboard() ^ capSq.ToBitboard()) | to.ToBitboard();

                    return ((Attacks(Rook, KingSquare(ActiveColor.Flip()), b) & Pieces(ActiveColor, Queen, Rook)) != 0)
                        | ((Attacks(Bishop, KingSquare(ActiveColor.Flip()), b) & Pieces(ActiveColor, Queen, Bishop)) != 0);

                case MoveType.Castling:
                    Square ksq = KingSquare(ActiveColor.Flip());
                    Square rto = (to > from ? Square.f1 : Square.d1).RelativeTo(ActiveColor);

                    return ((Attacks(Rook, rto) & ksq.ToBitboard()) != 0
                        || (Attacks(Rook, rto, Pieces() ^ from.ToBitboard() ^ to.ToBitboard()) & ksq.ToBitboard()) != 0);
            }
            return false;
        }
       
        public bool Capture(Move m)
        {
            Debug.Assert(IsLegal(m));

            return (PieceAt(m.ToSquare()) != Piece.None && m.TypeOf() != MoveType.Castling) || m.TypeOf() == MoveType.Promotion;
        }
       
        public bool CanCastle(CastlingRight cr)
        {
            return (CastlingRights & cr) != 0;
        }
       
        public bool CastlingImpeded(CastlingRight cr)
        {
            return (Pieces() & castlingPath[(int)cr]) != 0;
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
            sb.Append(EnPassantSquare == Square.None ? "-" : EnPassantSquare);
            sb.Append(' ');
            sb.Append(FiftyMoveCounter);
            sb.Append(' ');
            sb.Append(FullMove);

            return sb.ToString();
        }

        public void Set(Position pos, StateInfo newSt)
        {
            Set(pos.FEN(), newSt);

            state.Previous = pos.state.Previous;
        }

        public void Set(string fen, StateInfo newSt)
        {
            Debug.Assert(newSt != state);


            char token;
            StringReader stream = new(fen);

            state = newSt;
            Clear();

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
                    sq = sq.Shift(South).Shift(South);
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
            state.CastlingRights = 0;
            void AddCastlingRight(Color c, Square rsq)
            {
                Square ksq = KingSquare(c);
                bool kingSide = rsq > ksq;

                Square kto = (kingSide ? Square.g1 : Square.c1).RelativeTo(c);
                Square rto = (kingSide ? Square.f1 : Square.d1).RelativeTo(c);

                CastlingRight cr = MakeCastlingRight(c, kingSide ? CastlingRight.KingSide : CastlingRight.QueenSide);

                state.CastlingRights |= cr;
                castlingRightMask[(int)ksq] |= cr;
                castlingRightMask[(int)rsq] |= cr;
                castlingRookSquare[(int)cr] = rsq;
                castlingPath[(int)cr] = (Between(rsq, rto) | Between(ksq, kto) | kto.ToBitboard() | rto.ToBitboard()) & ~(ksq.ToBitboard() | rsq.ToBitboard());
            }

            while (stream.Extract(out token) && token != ' ')
            {
                if (token == '-')
                    break;

                Color c = char.IsUpper(token) ? White : Black;
                Square rsq = (char.ToLower(token) == 'k' ? Square.h1 : Square.a1).RelativeTo(c);

                AddCastlingRight(c, rsq);
            }

            //En passant square
            state.EnPassantSquare = Square.None;
            stream.Extract(out token);
            if (token != '-')
            {
                File f = (File)token - 'a';
                stream.Extract(out token);
                Rank r = (Rank)token - '1';

                state.EnPassantSquare = MakeSquare(f, r);
            }
            stream.Extract(out char _);

            stream.Extract(out state.FiftyMoveCounter);

            stream.Extract(out gamePly);
            gamePly = Math.Max(2 * (gamePly - 1), 0) + (ActiveColor == White ? 0 : 1);

            SetState(state);
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

            Debug.Assert(state != newSt);

            newSt.Previous = state;
            newSt.CastlingRights = state.CastlingRights;
            newSt.EnPassantSquare = state.EnPassantSquare;
            newSt.FiftyMoveCounter = state.FiftyMoveCounter;
            newSt.ZobristKey = state.ZobristKey;

            state = newSt;

            state.FiftyMoveCounter++;          
            gamePly++;

            Square from = m.FromSquare();
            Square to = m.ToSquare();
            MoveType type = m.TypeOf();

            Color us = ActiveColor;
            Color them = us.Flip();

            Piece piece = PieceAt(from);
            Piece captured = type == MoveType.EnPassant ? MakePiece(them, Pawn) : PieceAt(to);

            Debug.Assert(captured.GetPieceType() != King);

            if (type == MoveType.Castling)
            {
                Square kfrom = from;
                Square rfrom = to;

                HandleCastlingMove(us, kfrom, rfrom, out Square kto, out Square rto);

                state.ZobristKey ^= Zobrist.PieceKey(piece, kfrom) ^ Zobrist.PieceKey(piece, kto);
                state.ZobristKey ^= Zobrist.PieceKey(captured, rfrom) ^ Zobrist.PieceKey(captured, rto);

                captured = Piece.None;
            }

            if (captured != Piece.None)
            {
                Square capsq = to;

                if (Core.GetPieceType(captured) == Pawn)
                    if (type == MoveType.EnPassant)
                        capsq -= (int)PawnPush(us);

                RemovePieceAt(capsq);
                state.ZobristKey ^= Zobrist.PieceKey(captured, capsq);

                state.FiftyMoveCounter = 0;
            }

            if (state.EnPassantSquare != Square.None)
            {
                state.ZobristKey ^= Zobrist.EnPassantKey(state.EnPassantSquare);
                state.EnPassantSquare = Square.None;
            }

            if (state.CastlingRights != 0 && (castlingRightMask[(int)from] | castlingRightMask[(int)to]) != 0)
            {
                state.ZobristKey ^= Zobrist.CastlingKey(state.CastlingRights);
                state.CastlingRights &= ~(castlingRightMask[(int)from] | castlingRightMask[(int)to]);
                state.ZobristKey ^= Zobrist.CastlingKey(state.CastlingRights);
            }

            if (type != MoveType.Castling)
            {
                MovePiece(from, to);

                state.ZobristKey ^= Zobrist.PieceKey(piece, from) ^ Zobrist.PieceKey(piece, to);
            }

            if (Core.GetPieceType(piece) == Pawn)
            {
                if (((int)from ^ (int)to) == 16)
                {
                    state.EnPassantSquare = to.NegativeShift(PawnPush(us));
                    state.ZobristKey ^= Zobrist.EnPassantKey(state.EnPassantSquare);
                }
                else if (type == MoveType.Promotion)
                {
                    Piece promotionPiece = MakePiece(us, m.PromotionPiece());

                    RemovePieceAt(to);
                    SetPieceAt(promotionPiece, to);

                    state.ZobristKey ^= Zobrist.PieceKey(piece, to) ^ Zobrist.PieceKey(promotionPiece, to);
                }

                state.FiftyMoveCounter = 0;
            }

            state.CapturedPiece = captured;

            state.Checkers = 0;
            if (givesCheck)
                state.Checkers = AttackersTo(KingSquare(them)) & Pieces(us);

            ActiveColor = them;

            SetCheckInfo(state);
        }

        public void MakeNullMove(StateInfo newSt)
        {
            Debug.Assert(!IsCheck());
            Debug.Assert(newSt != state);

            newSt.Previous = state;
            newSt.Checkers = state.Checkers;
            newSt.CastlingRights = state.CastlingRights;
            newSt.EnPassantSquare = state.EnPassantSquare;
            newSt.ZobristKey = state.ZobristKey;
            newSt.FiftyMoveCounter = state.FiftyMoveCounter;
            
            newSt.FiftyMoveCounter++;
            gamePly++;


            if(newSt.EnPassantSquare != Square.None)
            {
                newSt.ZobristKey ^= Zobrist.EnPassantKey(newSt.EnPassantSquare);
                newSt.EnPassantSquare = Square.None;
            }

            newSt.ZobristKey ^= Zobrist.SideKey;
            ActiveColor = ActiveColor.Flip();

            SetCheckInfo(newSt);

            state = newSt;
        }

        public void TakebackNullMove()
        {
            Debug.Assert(state.Previous is not null);

            state = state.Previous;
            ActiveColor = ActiveColor.Flip();
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

            Debug.Assert(state.Previous is not null);

            ActiveColor = ActiveColor.Flip();

            Square from = move.FromSquare();
            Square to = move.ToSquare();
            MoveType type = move.TypeOf();

            Color us = ActiveColor;
            Piece captured = state.CapturedPiece;

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
                    capsq -= (int)PawnPush(us);

                SetPieceAt(captured, capsq);
            }

            if (type == MoveType.Castling)
            {
                HandleCastlingMove(us, from, to);
            }

            //Go back to the previous state object
            state = state.Previous ?? throw new Exception();

            gamePly--;
        }

        (Bitboard Blockers, Bitboard Pinners) CalculateBlockers(Square target, Color attackers)
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
                    if ((betweenPieces & Pieces(PieceAt(target).GetColor())) != 0)
                        pinners |= sniperSq.ToBitboard();
                }
            }

            return (blockers, pinners);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetPieceAt(Piece piece, Square square)
        {
            Bitboard bitboard = square.ToBitboard();

            board[(int)square] = piece;
            colorBitboards[(int)piece.GetColor()] |= bitboard;
            typeBitboards[(int)AllPieces] |= typeBitboards[(int)piece.GetPieceType()] |= bitboard; ;
            pieceCounts[(int)piece]++;

            PSQScore += PSQT.Get(piece, square);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void RemovePieceAt(Square square)
        {
            Piece piece = PieceAt(square);
            Bitboard bitboard = square.ToBitboard();

            board[(int)square] = Piece.None;
            colorBitboards[(int)piece.GetColor()] ^= bitboard;
            typeBitboards[(int)piece.GetPieceType()] ^= bitboard;
            typeBitboards[(int)AllPieces] ^= bitboard;
            pieceCounts[(int)piece]--;
            
            PSQScore -= PSQT.Get(piece, square);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void MovePiece(Square from, Square to)
        {
            Piece piece = PieceAt(from);
            Bitboard fromto = from.ToBitboard() | to.ToBitboard();

            board[(int)from] = Piece.None;
            board[(int)to] = piece;
            colorBitboards[(int)piece.GetColor()] ^= fromto;
            typeBitboards[(int)piece.GetPieceType()] ^= fromto;
            typeBitboards[(int)AllPieces] ^= fromto;
            
            PSQScore += PSQT.Get(piece, to) - PSQT.Get(piece, from);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetCheckInfo(StateInfo si)
        {
            (si.BlockersForKing[(int)White], si.Pinners[(int)Black]) = CalculateBlockers(KingSquare(White), Black);
            (si.BlockersForKing[(int)Black], si.Pinners[(int)White]) = CalculateBlockers(KingSquare(Black), White);

            Square ksq = KingSquare(ActiveColor.Flip());
            si.CheckSquares[(int)Pawn] = PawnAttacks(ActiveColor.Flip(), ksq);
            si.CheckSquares[(int)Knight] = Attacks(Knight, ksq);
            si.CheckSquares[(int)Bishop] = Attacks(Bishop, ksq, Pieces());
            si.CheckSquares[(int)Rook] = Attacks(Rook, ksq, Pieces());
            si.CheckSquares[(int)Queen] = si.CheckSquares[(int)Bishop] | si.CheckSquares[(int)Rook];
            si.CheckSquares[(int)King] = 0;
        }
        
        void SetState(StateInfo si)
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

            if (state.EnPassantSquare != Square.None)
                si.ZobristKey ^= Zobrist.EnPassantKey(state.EnPassantSquare);

            si.ZobristKey ^= Zobrist.CastlingKey(CastlingRights);
        }
      
        void Clear()
        {
            Array.Clear(board);
            Array.Clear(typeBitboards);
            Array.Clear(colorBitboards);
            Array.Clear(pieceCounts);

            Array.Clear(castlingRookSquare);
            Array.Clear(castlingRightMask);
            Array.Clear(castlingPath);

            ActiveColor = 0;
            gamePly = 0;
            PSQScore = Score.Zero;
        }

        public sealed class StateInfo
        {
            public StateInfo? Previous;
            public Piece CapturedPiece;
            public CastlingRight CastlingRights;
            public Square EnPassantSquare;
            public int FiftyMoveCounter;
            public Key ZobristKey;
            public Bitboard Checkers;

            public int repetition;

            public readonly Bitboard[] BlockersForKing = new Bitboard[(int)Color.NB];
            public readonly Bitboard[] Pinners = new Bitboard[(int)Color.NB];
            public readonly Bitboard[] CheckSquares = new Bitboard[(int)PieceType.NB];
        }

        static class Zobrist
        {
            static readonly Key[,] pieceKeys = new Key[(int)Piece.NB, (int)Square.NB];
            static readonly Key[] castlingKeys = new Key[(int)CastlingRight.NB];
            static readonly Key[] enPassantKeys = new Key[(int)File.NB];
            static readonly Key sideKey;
            public static Key PieceKey(Piece p, Square sq) => pieceKeys[(int)p, (int)sq];
            public static Key SideKey => sideKey;
            public static Key CastlingKey(CastlingRight cr) => castlingKeys[(int)cr];
            public static Key EnPassantKey(Square epSqr) => enPassantKeys[(int)epSqr.GetFile()];

            static Zobrist()
            {
                Random rand = new();

                for (int p = 0; p < pieceKeys.GetLength(0); p++)
                    for (Square s = Square.a1; s <= Square.h8; s++)
                        pieceKeys[p, (int)s] = (Key)rand.NextUInt64();

                sideKey = (Key)rand.NextUInt64();

                for (CastlingRight cr = CastlingRight.None; cr <= CastlingRight.All; cr++)
                    castlingKeys[(int)cr] = (Key)rand.NextUInt64();

                for (File f = 0; f <= File.h; f++)
                    enPassantKeys[(int)f] = (Key)rand.NextUInt64();
            }
        }

        static class Cuckoo
        {

        }
    }
}
