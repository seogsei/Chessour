using Chessour.Evaluation;
using Chessour.Search;
using Chessour.Utilities;
using System.Text;
using static Chessour.Bitboards;
using static Chessour.Color;
using static Chessour.Direction;
using static Chessour.Factory;
using static Chessour.PieceType;

namespace Chessour
{
    public sealed class Position
    {
        //Square centric piece representation
        private readonly Piece[] board = new Piece[(int)Square.NB];

        //Bitboards
        private Bitboard allPieces;
        private readonly Bitboard[] colorBitboards = new Bitboard[(int)Color.NB];
        private readonly Bitboard[] typeBitboards = new Bitboard[(int)PieceType.NB];

        //Castling extras
        private readonly CastlingRight[] castlingRightMasks = new CastlingRight[(int)Square.NB];
        private readonly Square[] castlingRookSquares = new Square[(int)CastlingRight.NB];
        private readonly Bitboard[] castlingPaths = new Bitboard[(int)CastlingRight.NB];
        private StateInfo state;

        public Color ActiveColor { get; private set; }
        public ScoreTuple PSQScore { get; private set; }
        public int PhaseValue { get; private set; }
        public int GamePly { get; private set; }
        public CastlingRight CastlingRights
        {
            get => state.CastlingRights;
            set => state.CastlingRights = value;
        }
        public Square EnPassantSquare
        {
            get => state.EnPassantSqaure;
            set => state.EnPassantSqaure = value;
        }
        public int FiftyMoveCounter
        {
            get => state.FiftyMove;
            set => state.FiftyMove = value;
        }
        public int PliesFromNull
        {
            get => state.PliesFromNull;
            set => state.PliesFromNull = value;
        }
        public Key PositionKey {
            get => state.PositionKey;
            set => state.PositionKey = value;
        }
        public Bitboard Checkers {
            get => state.Checkers; 
            set => state.Checkers = value;
        }
        public Piece CapturedPiece {
            get => state.CapturedPiece; 
            set => state.CapturedPiece = value;
        }
        public int Repetition {
            get => state.Repetition;
            set => state.Repetition = value;
        }
        public int FullMove => (GamePly / 2) + 1;

        public Position(StateInfo stateObject) : this(UCI.StartFEN, stateObject) { }

        public Position(string fen, StateInfo stateObject)
        {
            Set(fen, stateObject);

            Debug.Assert(state is not null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEmpty(Square square)
        {
            return PieceAt(square) == Piece.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Piece PieceAt(Square square)
        {
            return board[(int)square];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Square KingSquare(Color side)
        {
            return (colorBitboards[(int)side] & typeBitboards[(int)King]).LeastSignificantSquare();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard Pieces()
        {
            return allPieces;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard Pieces(Color color)
        {
            return colorBitboards[(int)color];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard Pieces(PieceType pieceType)
        {
            return typeBitboards[(int)pieceType];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard Pieces(PieceType pieceType1, PieceType pieceType2)
        {
            return Pieces(pieceType1) | Pieces(pieceType2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard Pieces(Color color, PieceType pieceType)
        {
            return Pieces(color) & Pieces(pieceType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard Pieces(Color color, PieceType pieceType1, PieceType pieceType2)
        {
            return Pieces(color) & Pieces(pieceType1, pieceType2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard AttackersTo(Square square)
        {
            return AttackersTo(square, Pieces());
        }

        public Bitboard AttackersTo(Square square, Bitboard occupancy)
        {
            Bitboard attackers = 0;
            attackers |= WhitePawnAttacks(square) & Pieces(Black, Pawn);
            attackers |= BlackPawnAttacks(square) & Pieces(White, Pawn);
            attackers |= KnightAttacks(square) & Pieces(Knight);
            attackers |= BishopAttacks(square, occupancy) & Pieces(Queen, Bishop);
            attackers |= RookAttacks(square, occupancy) & Pieces(Queen, Rook);
            attackers |= KingAttacks(square) & Pieces(King);
            return attackers;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard BlockersForKing(Color color)
        {
            return state.BlockersForKing[(int)color];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard CheckSquares(PieceType pieceType)
        {
            return state.CheckSquares[(int)pieceType];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bitboard Pinners(Color side)
        {
            return state.Pinners[(int)side];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCheck()
        {
            return state.Checkers != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanCastle(CastlingRight castlingRight)
        {
            return (CastlingRights & castlingRight) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CastlingImpeded(CastlingRight castlingRight)
        {
            return (Pieces() & castlingPaths[(int)castlingRight]) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Square CastlingRookSquare(CastlingRight castlingRight)
        {
            return castlingRookSquares[(int)castlingRight];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDraw()
        {
            return FiftyMoveCounter >= 100 || Repetition >= 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCapture(Move move)
        {
            Debug.Assert(IsLegal(move));

            return (!IsEmpty(move.DestinationSquare()) && move.Type() != MoveType.Castling) || move.Type() == MoveType.Promotion;
        }

        public bool IsPseudoLegal(Move move)
        {
            Color us = ActiveColor;
            Square from = move.OriginSquare();
            Square to = move.DestinationSquare();
            Piece pc = PieceAt(from);

            if (move.Type() != MoveType.Quiet)
            {
                var moves = IsCheck() ? MoveGenerators.Evasion.Generate(this, stackalloc MoveScore[MoveGenerators.MAX_MOVE_COUNT])
                                        : MoveGenerators.NonEvasion.Generate(this, stackalloc MoveScore[MoveGenerators.MAX_MOVE_COUNT]);

                foreach (var candMove in moves)
                    if (move == candMove.Move)
                        return true;
                return false;
            }

            if (Pieces(us).Contains(to))
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
                    && !(from + (int)us.PawnDirection() == to && IsEmpty(to))
                    && !((from + (int)us.PawnDirection() + (int)us.PawnDirection() == to)
                            && from.GetRank().RelativeTo(us) == Rank.R2
                            && IsEmpty(to)
                            && IsEmpty(to - (int)us.PawnDirection())))
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
            Color enemy = us.Flip();
            Square origin = move.OriginSquare();
            Square destination = move.DestinationSquare();
            MoveType type = move.Type();

            if (type == MoveType.EnPassant)
            {
                Square ksq = KingSquare(us);
                Square capsq = destination - (int)us.PawnDirection();
                Bitboard occupancy = (Pieces() ^ origin.ToBitboard() ^ capsq.ToBitboard()) | destination.ToBitboard();

                return (RookAttacks(ksq, occupancy) & Pieces(enemy, Queen, Rook)) == 0
                    && (BishopAttacks(ksq, occupancy) & Pieces(enemy, Queen, Bishop)) == 0;
            }

            if (type == MoveType.Castling)
            {
                destination = (destination > origin ? Square.g1 : Square.c1).RelativeTo(us);
                Direction step = destination > origin ? West : East;

                for (Square s = destination; s != origin; s += (int)step)
                    if ((AttackersTo(s) & Pieces(enemy)) != 0)
                        return false;

                return true;
            }

            if (PieceAt(origin).TypeOf() == King)
                return (AttackersTo(destination, Pieces() ^ origin.ToBitboard()) & Pieces(enemy)) == 0;

            return !BlockersForKing(us).Contains(origin) || Alligned(origin, destination, KingSquare(us));
        }

        public bool GivesCheck(Move move)
        {
            Color us = ActiveColor;
            Color enemy = us.Flip();
            Square ksq = KingSquare(enemy);
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

                    return RookAttacks(rto).Contains(ksq)
                        || RookAttacks(rto, Pieces() ^ origin.ToBitboard() ^ destination.ToBitboard()).Contains(ksq);

                default:
                    Square capSq = MakeSquare(destination.GetFile(), origin.GetRank());
                    Bitboard b = (Pieces() ^ origin.ToBitboard() ^ capSq.ToBitboard()) | destination.ToBitboard();

                    return ((RookAttacks(ksq, b) & Pieces(us, Queen, Rook)) != 0)
                        | ((BishopAttacks(ksq, b) & Pieces(us, Queen, Bishop)) != 0);
            }
        }

        public bool StaticExchangeEvaluationGE(Move move, int threshold = 0)
        {
            if (move.Type() != MoveType.Quiet)
                return 0 >= threshold;

            Square from = move.DestinationSquare();
            Square to = move.OriginSquare();

            int swap = Evaluation.Pieces.PieceValue(PieceAt(to)) - threshold;

            if (swap < 0)
                return false;

            swap = Evaluation.Pieces.PieceValue(PieceAt(from)) - swap;
            if (swap <= 0)
                return true;


            Color side = ActiveColor;
            Bitboard occupied = Pieces() ^ from.ToBitboard() ^ to.ToBitboard();
            Bitboard attackers = AttackersTo(to, occupied);
            Bitboard sideAttackers, bb;
            int result = 1;

            while (true)
            {
                side = side.Flip();
                attackers &= occupied;

                if ((sideAttackers = attackers & Pieces(side)) == 0)
                    break;


                if ((Pinners(side.Flip()) & occupied) != 0)
                {
                    sideAttackers &= ~BlockersForKing(side);

                    if (sideAttackers == 0)
                        break;
                }

                result ^= 1;

                if ((bb = sideAttackers & Pieces(Pawn)) != 0)
                {
                    if ((swap = Evaluation.Pieces.PawnValue - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                    attackers |= BishopAttacks(to, occupied) & Pieces(Bishop, Queen);
                }
                else if ((bb = sideAttackers & Pieces(Knight)) != 0)
                {
                    if ((swap = Evaluation.Pieces.KnightValue - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                }
                else if ((bb = sideAttackers & Pieces(Bishop)) != 0)
                {
                    if ((swap = Evaluation.Pieces.BishopValue - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                    attackers |= BishopAttacks(to, occupied) & Pieces(Bishop, Queen);
                }
                else if ((bb = sideAttackers & Pieces(Rook)) != 0)
                {
                    if ((swap = Evaluation.Pieces.RookValue - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                    attackers |= RookAttacks(to, occupied) & Pieces(Rook, Queen);
                }
                else if ((bb = sideAttackers & Pieces(Queen)) != 0)
                {
                    if ((swap = Evaluation.Pieces.QueenValue - swap) < result)
                        break;

                    occupied ^= bb.LeastSignificantBit();
                    attackers |= BishopAttacks(to, occupied) & Pieces(Bishop, Queen)
                              | RookAttacks(to, occupied) & Pieces(Rook, Queen);
                }
                else
                {
                    result ^= (attackers & ~Pieces(side)) != 0 ? 1 : 0;
                    break;
                }
            }
            return result > 0;
        }

        public string FEN()
        {
            StringBuilder sb = new();

            for (Rank r = Rank.R8; r >= Rank.R1; r--)
            {
                for (File f = File.a; f <= File.h; f++)
                {
                    int emptyCounter;
                    for (emptyCounter = 0; f <= File.h && IsEmpty(MakeSquare(f, r)); f++)
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
            Array.Copy(pos.castlingRightMasks, castlingRightMasks, castlingRightMasks.Length);
            Array.Copy(pos.castlingRookSquares, castlingRookSquares, castlingRookSquares.Length);
            Array.Copy(pos.castlingPaths, castlingPaths, castlingPaths.Length);
            state = pos.state;
            ActiveColor = pos.ActiveColor;
            GamePly = pos.GamePly;
            PhaseValue = pos.PhaseValue;
            PSQScore = pos.PSQScore;
        }

        public void Set(string fen, StateInfo newState)
        {
            state = newState;
            Set(fen);
        }

        public void Set(string fen)
        {
            Reset();
            state.Reset();

            string[] parts = fen.Split();

            //Piece Placement
            Square square = Square.a8;
            foreach (char token in parts[0])
            {
                if (char.IsDigit(token))
                {
                    square += token - '0';
                }
                else if (token == '/')
                {
                    square += (int)South * 2;
                }
                else
                {
                    Color c = char.IsUpper(token) ? White : Black;
                    PieceType pt = (PieceType)" pnbrqk".IndexOf(char.ToLower(token));

                    SetPieceAt(MakePiece(c, pt), square++);
                }
            }

            //Active Color
            ActiveColor = White;
            if (parts.Length > 1 && parts[1] == "b")
                ActiveColor = Black;

            //Castling Rights
            CastlingRights = 0;
            void AddCastlingRight(Color side, Square rookSquare)
            {
                Square kingSquare = KingSquare(side);
                bool kingSide = rookSquare > kingSquare;

                Square kto = (kingSide ? Square.g1 : Square.c1).RelativeTo(side);
                Square rto = (kingSide ? Square.f1 : Square.d1).RelativeTo(side);

                CastlingRight cr = MakeCastlingRight(side, kingSide ? CastlingRight.KingSide : CastlingRight.QueenSide);

                CastlingRights |= cr;
                castlingRightMasks[(int)kingSquare] |= cr;
                castlingRightMasks[(int)rookSquare] |= cr;
                castlingRookSquares[(int)cr] = rookSquare;
                castlingPaths[(int)cr] = (Between(rookSquare, rto) | Between(kingSquare, kto) | kto.ToBitboard() | rto.ToBitboard()) & ~(kingSquare.ToBitboard() | rookSquare.ToBitboard());
            }

            if (parts.Length > 2 && parts[2] != "-")
                foreach (char token in parts[2])
                {
                    Color side = char.IsUpper(token) ? White : Black;
                    Square rookSquare = (char.ToLower(token) == 'k' ? Square.h1 : Square.a1).RelativeTo(side);

                    AddCastlingRight(side, rookSquare);
                }

            //En passant square
            EnPassantSquare = Square.None;
            if (parts.Length > 3 && parts[3] != "-")
            {
                File file = (File)parts[3][0];
                Rank rank = (Rank)parts[3][1];

                EnPassantSquare = MakeSquare(file, rank);
            }

            FiftyMoveCounter = 0;
            if (parts.Length > 4 && int.TryParse(parts[4], out int fiftyMove))
                FiftyMoveCounter = fiftyMove;

            GamePly = 1;
            if (parts.Length > 5 && int.TryParse(parts[5], out int moveCounter))
                GamePly = Math.Max(2 * (moveCounter - 1), 0) + (ActiveColor == White ? 0 : 1);


            PositionKey = CalculatePositionKey();

            Checkers = AttackersTo(KingSquare(ActiveColor)) & Pieces(ActiveColor.Flip());
            SetCheckInfo(state);
        }

        private void Reset()
        {
            ActiveColor = default;
            GamePly = default;
            PSQScore = default;
            PhaseValue = default;
            Array.Clear(board);
            allPieces = default;
            Array.Clear(typeBitboards);
            Array.Clear(colorBitboards);
            Array.Clear(castlingRookSquares);
            Array.Clear(castlingRightMasks);
            Array.Clear(castlingPaths);
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

            newState.Previous = state;
            newState.PositionKey = state.PositionKey;
            newState.CastlingRights = state.CastlingRights;
            newState.EnPassantSqaure = state.EnPassantSqaure;
            newState.FiftyMove = state.FiftyMove;
            newState.PliesFromNull = state.PliesFromNull;

            state = newState;

            FiftyMoveCounter++;
            PliesFromNull++;
            GamePly++;

            Square origin = move.OriginSquare();
            Square destination = move.DestinationSquare();
            MoveType moveType = move.Type();

            Color us = ActiveColor;
            Color them = us.Flip();

            Piece piece = PieceAt(origin);
            Piece captured = moveType == MoveType.EnPassant ? MakePiece(them, Pawn) : PieceAt(destination);

            Key key = PositionKey;

            Debug.Assert(captured.TypeOf() != King);

            if (moveType == MoveType.Castling)
            {
                MakeCastlingMove(us, origin, destination, out Square kingDestination, out Square rookDestination);

                key ^= Zobrist.PieceKey(piece, origin) ^ Zobrist.PieceKey(piece, kingDestination);
                key ^= Zobrist.PieceKey(captured, destination) ^ Zobrist.PieceKey(captured, rookDestination);

                captured = Piece.None;
            }

            if (captured != Piece.None)
            {
                Square capsq = destination;

                if (captured.TypeOf() == Pawn)
                    if (moveType == MoveType.EnPassant)
                        capsq -= (int)us.PawnDirection();

                RemovePieceAt(capsq);
                key ^= Zobrist.PieceKey(captured, capsq);

                state.FiftyMove = 0;
            }

            if (EnPassantSquare != Square.None)
            {
                key ^= Zobrist.EnPassantKey(EnPassantSquare);
                EnPassantSquare = Square.None;
            }

            if (CastlingRights != CastlingRight.None && (castlingRightMasks[(int)origin] | castlingRightMasks[(int)destination]) != 0)
            {
                key ^= Zobrist.CastlingKey(CastlingRights);
                CastlingRights &= ~(castlingRightMasks[(int)origin] | castlingRightMasks[(int)destination]);
                key ^= Zobrist.CastlingKey(CastlingRights);
            }

            if (moveType != MoveType.Castling)
            {
                MovePiece(origin, destination);
                key ^= Zobrist.PieceKey(piece, origin) ^ Zobrist.PieceKey(piece, destination);
            }

            if (piece.TypeOf() == Pawn)
            {
                if (((int)origin ^ (int)destination) == 16)
                {
                    EnPassantSquare = destination - (int)us.PawnDirection();
                    key ^= Zobrist.EnPassantKey(EnPassantSquare);
                }
                else if (moveType == MoveType.Promotion)
                {
                    Piece promotionPiece = MakePiece(us, move.PromotionPiece());

                    RemovePieceAt(destination);
                    SetPieceAt(promotionPiece, destination);
                    key ^= Zobrist.PieceKey(piece, destination) ^ Zobrist.PieceKey(promotionPiece, destination);
                }

                state.FiftyMove = 0;
            }

            CapturedPiece = captured;
            Checkers = givesCheck ? AttackersTo(KingSquare(them)) & Pieces(us) : 0;
            
            ActiveColor = them;
            PositionKey = key ^= Zobrist.SideKey();

            SetCheckInfo(state);

            //Repetition info
            Repetition = 0;
            int plies = Math.Min(FiftyMoveCounter, PliesFromNull);

            //It requires atleast 4 plies to repeat
            if (plies >= 4)
            {
                StateInfo repeatCandidate = state.Previous.Previous!;

                for (int i = 4; i < plies; i += 2)
                {
                    repeatCandidate = repeatCandidate.Previous!.Previous!;

                    if (key == repeatCandidate.PositionKey)
                    {
                        Repetition = repeatCandidate.Repetition + 1;
                        break;
                    }
                }
            }

            Debug.Assert(PositionKey == CalculatePositionKey(),
                $""""
                Current Key:{PositionKey}
                Expected:   {CalculatePositionKey()}
                Last Move: {UCI.Move(move)}
                """");
        }

        public void MakeNullMove(StateInfo newSt)
        {
            Debug.Assert(Checkers == 0);

            newSt.Previous = state;
            newSt.PositionKey = state.PositionKey;
            newSt.CastlingRights = state.CastlingRights;
            newSt.EnPassantSqaure = state.EnPassantSqaure;
            newSt.FiftyMove = state.FiftyMove;

            state = newSt;

            PliesFromNull = 0;
            FiftyMoveCounter++;
            GamePly++;

            if (EnPassantSquare!= Square.None)
            {
                PositionKey ^= Zobrist.EnPassantKey(state.EnPassantSqaure);
                EnPassantSquare = Square.None;
            }

            CapturedPiece = Piece.None;

            ActiveColor = ActiveColor.Flip();
            PositionKey ^= Zobrist.SideKey();

            SetCheckInfo(state);
            Checkers = 0;
            Repetition = 0;
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
            Piece captured = state.CapturedPiece;

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
                    capsq -= (int)us.PawnDirection();

                SetPieceAt(captured, capsq);
            }

            if (type == MoveType.Castling)
            {
                MakeCastlingMove(us, origin, destination, out var _, out var _);
            }

            //Go back to the previous state object
            state = state.Previous!;

            GamePly--;
        }

        public void TakebackNullMove()
        {
            state = state.Previous!;
            ActiveColor = ActiveColor.Flip();
            GamePly--;
        }

        //Did this position happen in this many moves?
        public bool HasRepeated(int ply)
        {
            if (ply < 4) 
                return false;

            Key positionKey = state.PositionKey;
            StateInfo repeatCandidate = state.Previous!.Previous!;

            for(int i = 4; i < ply; i += 2)
            {
                repeatCandidate = repeatCandidate.Previous!.Previous!;

                if (positionKey == repeatCandidate.PositionKey)
                    return true;
            }
            return false;
        }

        private (Bitboard, Bitboard) SliderBlockers(Bitboard sliders, Square target)
        {
            Bitboard blockers = 0;
            Bitboard pinners = 0;

            Bitboard snipers = sliders &
                ((RookAttacks(target) & Pieces(Rook, Queen)) 
                | (BishopAttacks(target) & Pieces(Bishop, Queen)));


            Bitboard occupancy = Pieces() ^ snipers;

            foreach (Square sniper in snipers)
            {
                Bitboard betweenPieces = Between(target, sniper) & occupancy;

                //There is exactly one piece
                if (betweenPieces != 0 && !betweenPieces.MoreThanOne())
                {
                    //This means there is only one piece between the slider and the target
                    //Which makes the piece a blocker for target
                    blockers |= betweenPieces;

                    //If the blocker is same color as the piece on target square that makes the sniper a pinner
                    //And blocker a pinned piece
                    if ((betweenPieces & Pieces(PieceAt(target).ColorOf())) != 0)
                        pinners |= sniper.ToBitboard();
                }
            }

            return (blockers, pinners);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetCheckInfo(StateInfo state)
        {
            (state.BlockersForKing[(int)White], state.Pinners[(int)Black]) = SliderBlockers(Pieces(Black), KingSquare(White));
            (state.BlockersForKing[(int)Black], state.Pinners[(int)White]) = SliderBlockers(Pieces(White), KingSquare(Black));

            Color enemyColor = ActiveColor.Flip();
            Square enemyKingSquare = KingSquare(enemyColor);

            state.CheckSquares[(int)Pawn] = PawnAttacks(enemyColor, enemyKingSquare);
            state.CheckSquares[(int)Knight] = KnightAttacks(enemyKingSquare);
            state.CheckSquares[(int)Bishop] = BishopAttacks(enemyKingSquare, Pieces());
            state.CheckSquares[(int)Rook] = RookAttacks(enemyKingSquare, Pieces());
            state.CheckSquares[(int)Queen] = state.CheckSquares[(int)Bishop] | state.CheckSquares[(int)Rook];
            state.CheckSquares[(int)King] = 0;
        }

        public Key CalculatePositionKey()
        {
            Key key = 0;

            foreach (Square square in Pieces())
                key ^= Zobrist.PieceKey(PieceAt(square), square);

            if (ActiveColor == Black)
                key ^= Zobrist.SideKey();

            if (EnPassantSquare != Square.None)
                key ^= Zobrist.EnPassantKey(EnPassantSquare);

            return key ^= Zobrist.CastlingKey(CastlingRights); 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetPieceAt(Piece piece, Square square)
        {
            Bitboard squareBB = square.ToBitboard();

            allPieces |= squareBB;
            typeBitboards[(int)piece.TypeOf()] |= squareBB;
            colorBitboards[(int)piece.ColorOf()] |= squareBB;

            board[(int)square] = piece;
            PSQScore += PSQT.Get(piece, square);
            PhaseValue += Phase.PhaseValue(piece);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemovePieceAt(Square square)
        {
            Piece piece = board[(int)square];
            Bitboard squareBB = square.ToBitboard();

            allPieces ^= squareBB;
            typeBitboards[(int)piece.TypeOf()] ^= squareBB;
            colorBitboards[(int)piece.ColorOf()] ^= squareBB;


            board[(int)square] = Piece.None;
            PSQScore -= PSQT.Get(piece, square);
            PhaseValue -= Phase.PhaseValue(piece);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MovePiece(Square from, Square to)
        {
            Piece piece = board[(int)from];
            Bitboard fromto = from.ToBitboard() | to.ToBitboard();

            allPieces ^= fromto;
            typeBitboards[(int)piece.TypeOf()] ^= fromto;
            colorBitboards[(int)piece.ColorOf()] ^= fromto;

            board[(int)from] = 0;
            board[(int)to] = piece;
            PSQScore += PSQT.Get(piece, to) - PSQT.Get(piece, from);
        }

        public override string ToString()
        {
            StringBuilder sb = new(1024);

            sb.AppendLine(" +---+---+---+---+---+---+---+---+");

            for (Rank r = Rank.R8; r >= Rank.R1; r--)
            {
                for (File f = File.a; f <= File.h; f++)
                    sb.Append(" | ").Append(PieceAt(MakeSquare(f,r)).PieceToChar());

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
            private static readonly Key[][] pieceKeys = new Key[(int)Piece.NB][];
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
                return pieceKeys[(int)piece][(int)square];
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

                pieceKeys[(int)Piece.None] = new Key[(int)Square.NB];

                for (Piece whitePiece = Piece.WhitePawn; whitePiece <= Piece.WhiteKing; whitePiece++)
                {
                    Piece blackPiece = whitePiece.FlipColor();

                    pieceKeys[(int)whitePiece] = new Key[(int)Square.NB];
                    pieceKeys[(int)blackPiece] = new Key[(int)Square.NB];

                    for (Square square = Square.a1; square <= Square.h8; square++)
                    {
                        pieceKeys[(int)whitePiece][(int)square] = (Key)prng.NextUInt64();
                        pieceKeys[(int)blackPiece][(int)square] = (Key)prng.NextUInt64();
                    }
                }

                sideKey = (Key)prng.NextUInt64();

                for (CastlingRight castlingRight = CastlingRight.None; castlingRight < CastlingRight.NB; castlingRight++)
                    castlingKeys[(int)castlingRight] = (Key)prng.NextUInt64();

                for (File file = File.a; file <= File.h; file++)
                    enpassantKeys[(int)file] = (Key)prng.NextUInt64();
            }
        }

        public sealed class StateInfo
        {
            internal StateInfo? Previous { get; set; }
            internal int FiftyMove { get; set; }
            internal int PliesFromNull { get; set; }
            internal int Repetition { get; set; }
            internal CastlingRight CastlingRights { get; set; }
            internal Square EnPassantSqaure { get; set; }
            internal Piece CapturedPiece { get; set; }
            internal Bitboard Checkers { get; set; }
            internal Key PositionKey { get; set; }
            internal Bitboard[] BlockersForKing { get; } = new Bitboard[(int)Color.NB];
            internal Bitboard[] Pinners { get; } = new Bitboard[(int)Color.NB];
            internal Bitboard[] CheckSquares { get; } = new Bitboard[(int)PieceType.NB];

            internal void Reset()
            {
                Previous = default;
                FiftyMove = default;
                PliesFromNull = default;
                Repetition = default;
                CastlingRights = default;
                EnPassantSqaure = default;
                CapturedPiece = default;
                Checkers = default;
                PositionKey = default;
                Array.Clear(BlockersForKing);
                Array.Clear(Pinners);
                Array.Clear(CheckSquares);
            }
        }
    }
}
