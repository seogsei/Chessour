using Chessour.Types;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Chessour.Bitboards;
using static Chessour.Types.Color;
using static Chessour.Types.Direction;
using static Chessour.Types.Factory;
using static Chessour.Types.PieceType;


namespace Chessour
{
    public sealed partial class Position : IPositionInfo
    {
        public static void Init() { Zobrist.Init(); }


        readonly Piece[] board = new Piece[(int)Square.NB];
        readonly Bitboard[] typeBB = new Bitboard[(int)PieceType.NB];
        readonly Bitboard[] colorBB = new Bitboard[(int)Color.NB];
        readonly int[] pieceCount = new int[(int)Piece.NB];
        readonly CastlingRight[] castlingRightMask = new CastlingRight[(int)Square.NB];
        readonly Square[] castlingRookSquare = new Square[(int)CastlingRight.NB];
        readonly Bitboard[] castlingPath = new Bitboard[(int)CastlingRight.NB];
        Score materialScore;
        Color activeColor;
        StateInfo state;
        int gamePly;

        public Color ActiveColor => activeColor;
        public CastlingRight CastlingRights => state.CastlingRights;
        public Square EnPassantSquare => state.EnPassantSquare;
        public int FiftyMoveCounter => state.FiftyMoveCounter;
        public Key ZobristKey => state.ZobristKey;
        public Bitboard Checkers => state.Checkers;
        public Piece CapturedPiece => state.CapturedPiece;

        public int FullMoveCounter => (gamePly / 2) + 1;
        public Score PSQScore { get => materialScore; }

        public Piece PieceAt(Square s)
        {
            Debug.Assert(s.IsValid());
            return board[(int)s];
        }
        public Square KingSquare(Color c)
        {
            return (colorBB[(int)c] & typeBB[(int)King]).LeastSignificantSquare();
        }
        public Bitboard Pieces()
        {
            return colorBB[(int)White] | colorBB[(int)Black];
        }
        public Bitboard Pieces(Color c)
        {
            return colorBB[(int)c];
        }
        public Bitboard Pieces(PieceType pt)
        {
            return typeBB[(int)pt];
        }
        public Bitboard Pieces(PieceType pt1, PieceType pt2)
        {
            return (typeBB[(int)pt1] | typeBB[(int)pt2]);
        }
        public Bitboard Pieces(Color c, PieceType pt)
        {
            return colorBB[(int)c] & typeBB[(int)pt];
        }
        public Bitboard Pieces(Color c, PieceType pt1, PieceType pt2)
        {
            return colorBB[(int)c] & (typeBB[(int)pt1] | typeBB[(int)pt2]);
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
            return pieceCount[(int)pc];
        }
        public bool IsCheck()
        {
            return state.Checkers != 0;
        }
        public bool IsPseudoLegal(Move m)
        {
            Color us = activeColor;
            Square from = m.FromSquare();
            Square to = m.ToSquare();
            Piece pc = PieceAt(from);

            if (m.TypeOf() != MoveType.Quiet)
                return new MoveList(IsCheck() ? GenerationType.Evasions : GenerationType.NonEvasions,
                                    this,
                                    stackalloc MoveScore[MoveList.MaxMoveCount]).Contains(m);

            if ((Pieces(us) & MakeBitboard(to)) != 0)
                return false;

            if (pc == Piece.None || pc.ColorOf() != us)
                return false;

            if (m.PromotionPiece() != Knight)
                return false;


            if (pc.TypeOf() == Pawn)
            {
                if (((Bitboard.Rank8 | Bitboard.Rank1) & to.ToBitboard()) != 0)
                    return false;

                if (!((PawnAttacks(us, from) & Pieces(us.Opposite()) & to.ToBitboard()) != 0)
                    && !(from.Shift(us.PawnPush()) == to && PieceAt(to) == Piece.None)
                    && !((from.Shift(us.PawnPush()).Shift(us.PawnPush()) == to)
                            && from.RankOf().RelativeTo(us) == Rank.R2
                            && PieceAt(to) == Piece.None
                            && PieceAt(to.NegativeShift(us.PawnPush())) == Piece.None))
                    return false;
            }
            else if (((Attacks(pc.TypeOf(), from, Pieces())) & MakeBitboard(to)) == 0)
                return false;


            if (IsCheck())
            {
                if (pc.TypeOf() != King)
                {
                    if (Checkers.MoreThanOne())
                        return false;

                    if (((Between(KingSquare(us), Checkers.LeastSignificantSquare()) | Checkers) & MakeBitboard(to)) == 0)
                        return false;
                }
                else if ((AttackersTo(to, Pieces() ^ MakeBitboard(from)) & Pieces(us.Opposite())) != 0)
                    return false;
            }

            return true;
        }
        public bool IsLegal(Move m)
        {
            Color us = ActiveColor;
            Color them = ActiveColor.Opposite();

            Square from = m.FromSquare();
            Square to = m.ToSquare();
            MoveType type = m.TypeOf();

            if (type == MoveType.EnPassant)
            {
                Square ksq = KingSquare(us);
                Square capsq = to.NegativeShift(us.PawnPush());
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

            if (PieceAt(from).TypeOf() == King)
                return (AttackersTo(to, Pieces() ^ from.ToBitboard()) & Pieces(them)) == 0;

            return (BlockersForKing(us) & from.ToBitboard()) == 0
                || Alligned(from, to, KingSquare(us));
        }
        public bool GivesCheck(Move m)
        {
            Square from = m.FromSquare();
            Square to = m.ToSquare();

            if ((CheckSquares(PieceAt(from).TypeOf()) & MakeBitboard(to)) != 0)
                return true;

            if (((BlockersForKing(activeColor.Opposite()) & MakeBitboard(from)) != 0)
                && !Alligned(from, to, KingSquare(activeColor.Opposite())))
                return true;

            switch (m.TypeOf())
            {
                case MoveType.Quiet:
                    return false;
                case MoveType.Promotion:
                    return (Attacks(m.PromotionPiece(), to, Pieces() ^ MakeBitboard(from)) & MakeBitboard(KingSquare(activeColor.Opposite()))) != 0;

                case MoveType.EnPassant:
                    Square capSq = MakeSquare(to.FileOf(), from.RankOf());
                    Bitboard b = (Pieces() ^ MakeBitboard(from) ^ MakeBitboard(capSq)) | MakeBitboard(to);

                    return ((Attacks(Rook, KingSquare(activeColor.Opposite()), b) & Pieces(activeColor, Queen, Rook)) != 0)
                        | ((Attacks(Bishop, KingSquare(activeColor.Opposite()), b) & Pieces(activeColor, Queen, Bishop)) != 0);

                case MoveType.Castling:
                    Square ksq = KingSquare(activeColor.Opposite());
                    Square rto = (to > from ? Square.f1 : Square.d1).RelativeTo(activeColor);

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
        public Square CastlingRookSquare(CastlingRight cr)
        {
            return castlingRookSquare[(int)cr];
        }

        public Position(StateInfo newSt)
        {
            state = newSt;
        }
        public Position(string fen, StateInfo newSt)
        {
            Set(fen, newSt);

            if (state is null)
                throw new Exception();
        }
        public string FEN()
        {
            return Chessour.FEN.Generate(this);
        }

        public void Set(string fen, StateInfo newSt)
        {
            Clear();

            var fenParts = fen.Split(' ');

            state = newSt;

            //Piece Placement
            Square sq = Square.a8;
            foreach (char token in fenParts[0])
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
            activeColor = fenParts[1] == "w" ? White : Black;

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

            foreach (char token in fenParts[2])
            {
                if (token == '-')
                    break;

                Color c = char.IsUpper(token) ? White : Black;
                Square rsq = (char.ToLower(token) == 'k' ? Square.h1 : Square.a1).RelativeTo(c);

                AddCastlingRight(c, rsq);
            }

            //En passant square
            state.EnPassantSquare = Square.None;
            if (fenParts[3] != "-")
            {
                File f = (File)fenParts[3][0] - 'a';
                Rank r = (Rank)fenParts[3][1] - '1';

                state.EnPassantSquare = MakeSquare(f, r);
            }

            state.FiftyMoveCounter = int.Parse(fenParts[4]);
            gamePly = int.Parse(fenParts[5]);

            gamePly = Math.Max(2 * (gamePly - 1), 0) + (activeColor == White ? 0 : 1);


            SetState(state);
        }
        public void Set(Position pos, StateInfo newSt)
        {
            Set(pos.FEN(), newSt);

            state.Previous = pos.state.Previous;
        }
        public void MakeMove(Move m, StateInfo newSt) => MakeMove(m, newSt, GivesCheck(m));
        internal void MakeMove(Move m, StateInfo newSt, bool givesCheck)
        {
            Debug.Assert(state != newSt);

            newSt.Checkers = 0;
            newSt.Previous = state;
            newSt.LastMove = m;
            newSt.CastlingRights = state.CastlingRights;
            newSt.EnPassantSquare = state.EnPassantSquare;
            newSt.ZobristKey = state.ZobristKey;
            newSt.FiftyMoveCounter = state.FiftyMoveCounter;
            newSt.FiftyMoveCounter++;
            gamePly++;

            state = newSt;

            Square from = m.FromSquare();
            Square to = m.ToSquare();
            MoveType type = m.TypeOf();

            Color us = ActiveColor;
            Color them = us.Opposite();

            Piece piece = PieceAt(from);
            Piece captured = type == MoveType.EnPassant ? MakePiece(them, Pawn) : PieceAt(to);

            Debug.Assert(captured.TypeOf() != King, FEN());

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

                if (captured.TypeOf() == Pawn)
                    if (type == MoveType.EnPassant)
                        capsq -= (int)us.PawnPush();

                RemovePieceAt(capsq);
                state.ZobristKey ^= Zobrist.PieceKey(captured, capsq);

                state.FiftyMoveCounter = 0;
            }

            if (state.EnPassantSquare != Square.None)
            {
                state.ZobristKey ^= Zobrist.EnPassantKey(EnPassantSquare);
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

            if (piece.TypeOf() == Pawn)
            {
                if (((int)from ^ (int)to) == 16)
                {
                    state.EnPassantSquare = to.NegativeShift(us.PawnPush());
                    state.ZobristKey ^= Zobrist.EnPassantKey(newSt.EnPassantSquare);
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

            if (givesCheck)
                state.Checkers = AttackersTo(KingSquare(them)) & Pieces(us);

            activeColor = them;

            SetCheckInfo(state);
        }
        public void Takeback()
        {
            if (state.Previous is null)
                throw new InvalidOperationException();

            activeColor = activeColor.Opposite();
            gamePly--;

            Move m = state.LastMove;

            Square from = m.FromSquare();
            Square to = m.ToSquare();
            MoveType type = m.TypeOf();

            Color us = activeColor;
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
                {
                    capsq -= (int)us.PawnPush();
                }

                SetPieceAt(captured, capsq);
            }

            if (type == MoveType.Castling)
            {
                HandleCastlingMove(us, from, to, out _, out _, true);
            }

            //Go back to the previous state object
            state = state.Previous;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetPieceAt(Piece p, Square s)
        {
            Debug.Assert(s.IsValid() && PieceAt(s) == Piece.None);

            Bitboard b = s.ToBitboard();

            board[(int)s] = p;
            typeBB[(int)p.TypeOf()] ^= b;
            colorBB[(int)p.ColorOf()] ^= b;
            pieceCount[(int)p]++;
            materialScore += PSQT.Get(p, s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void RemovePieceAt(Square s)
        {
            Debug.Assert(s.IsValid() && PieceAt(s) != Piece.None);

            Piece p = PieceAt(s);

            board[(int)s] = Piece.None;
            typeBB[(int)p.TypeOf()] ^= s.ToBitboard();
            colorBB[(int)p.ColorOf()] ^= s.ToBitboard();
            pieceCount[(int)p]--;
            materialScore -= PSQT.Get(p, s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void MovePiece(Square from, Square to)
        {
            Debug.Assert(from.IsValid() && to.IsValid() && PieceAt(from) != Piece.None && from != to);

            Piece p = PieceAt(from);
            Bitboard fromto = from.ToBitboard() | to.ToBitboard();
            board[(int)from] = Piece.None;
            board[(int)to] = p;
            typeBB[(int)p.TypeOf()] ^= fromto;
            colorBB[(int)p.ColorOf()] ^= fromto;
            materialScore += PSQT.Get(p, to) - PSQT.Get(p, from);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void HandleCastlingMove(Color us, Square kfrom, Square rfrom, out Square kto, out Square rto, bool undo = false)
        {
            bool kingSide = rfrom > kfrom;

            kto = (kingSide ? Square.g1 : Square.c1).RelativeTo(us);
            rto = (kingSide ? Square.f1 : Square.d1).RelativeTo(us);

            RemovePieceAt(undo ? kto : kfrom);
            RemovePieceAt(undo ? rto : rfrom);

            SetPieceAt(MakePiece(us, King), undo ? kfrom : kto);
            SetPieceAt(MakePiece(us, Rook), undo ? rfrom : rto);
        }
        void SetCheckInfo(StateInfo si)
        {
            (si.BlockersForKing[(int)White], si.Pinners[(int)Black]) = CalculateBlockers(KingSquare(White), Pieces(Black));
            (si.BlockersForKing[(int)Black], si.Pinners[(int)White]) = CalculateBlockers(KingSquare(Black), Pieces(White));

            Square ksq = KingSquare(activeColor.Opposite());
            si.CheckSquares[(int)Pawn] = PawnAttacks(activeColor.Opposite(), ksq);
            si.CheckSquares[(int)Knight] = Attacks(Knight, ksq);
            si.CheckSquares[(int)Bishop] = Attacks(Bishop, ksq, Pieces());
            si.CheckSquares[(int)Rook] = Attacks(Rook, ksq, Pieces());
            si.CheckSquares[(int)Queen] = si.CheckSquares[(int)Bishop] | si.CheckSquares[(int)Rook];
            si.CheckSquares[(int)King] = 0;
        }
        void SetState(StateInfo si)
        {
            si.ZobristKey = 0ul;
            si.Checkers = AttackersTo(KingSquare(activeColor)) & Pieces(activeColor.Opposite());

            SetCheckInfo(si);

            foreach (Square sq in Pieces())
            {
                Piece pc = PieceAt(sq);
                si.ZobristKey ^= Zobrist.PieceKey(pc, sq);
            }

            if (activeColor == Color.Black)
                si.ZobristKey ^= Zobrist.SideKey;

            if (state.EnPassantSquare != Square.None)
                si.ZobristKey ^= Zobrist.EnPassantKey(state.EnPassantSquare);

            si.ZobristKey ^= Zobrist.CastlingKey(CastlingRights);
        }
        (Bitboard Blockers, Bitboard Pinners) CalculateBlockers(Square target, Bitboard attackers)
        {
            Bitboard blockers = 0;
            Bitboard pinners = 0;

            Bitboard snipers = ((Attacks(Rook, target) & Pieces(Queen, Rook))
                               | (Attacks(Bishop, target) & Pieces(Queen, Bishop)))
                                & attackers;
            Bitboard occupancy = Pieces();

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

        void Clear()
        {
            Array.Clear(board);
            Array.Clear(typeBB);
            Array.Clear(colorBB);
            Array.Clear(pieceCount);
            Array.Clear(castlingRookSquare);
            Array.Clear(castlingRightMask);
            Array.Clear(castlingPath);
            activeColor = 0;
            gamePly = 0;
            materialScore = Score.Zero;
        }

        static class Zobrist
        {
            static readonly Key[,] pieceKeys = new Key[15, (int)Square.NB];
            static readonly Key sideKey;
            static readonly Key[] castlingKeys = new Key[(int)CastlingRight.NB];
            static readonly Key[] enPassantKeys = new Key[8];

            public static Key PieceKey(Piece p, Square sq) => pieceKeys[(int)p, (int)sq];
            public static Key SideKey { get => sideKey; }
            public static Key CastlingKey(CastlingRight cr) => castlingKeys[(int)cr];
            public static Key EnPassantKey(Square epSqr) => enPassantKeys[(int)epSqr.FileOf()];

            public static void Init() { }
            static Zobrist()
            {
                Random rand = new();
                ulong RandomUInt64()
                {
                    Span<byte> buffer = stackalloc byte[8];
                    rand.NextBytes(buffer);
                    return BitConverter.ToUInt64(buffer);
                }

                for (int p = 0; p < pieceKeys.GetLength(0); p++)
                    for (Square s = Square.a1; s <= Square.h8; s++)
                        pieceKeys[p, (int)s] = (Key)RandomUInt64();

                sideKey = (Key)RandomUInt64();

                for (CastlingRight cr = CastlingRight.None; cr <= CastlingRight.All; cr++)
                    castlingKeys[(int)cr] = (Key)RandomUInt64();

                for (File f = 0; f <= File.h; f++)
                    enPassantKeys[(int)f] = (Key)RandomUInt64();
            }
        }
    }
}
