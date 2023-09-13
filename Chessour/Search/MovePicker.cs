using Chessour.Evaluation;
using Chessour.Utilities;

namespace Chessour.Search
{
    internal ref struct MovePicker
    {
        private enum Stage
        {
            MainTT, CaptureGenerate, GoodCaptures, QuietGenerate, Quiet, BadCaptures,
            EvasionTT, EvasionGenerate, Evasions,
            QSearchTT, QCaptureGenerate, QCapture
        }

        private MovePicker(Stage stage, Position position, Move ttMove, ButterflyTable butterfly, Square refutationSquare, Span<MoveScore> buffer)
        {
            this.position = position;
            this.ttMove = ttMove;
            this.butterfly = butterfly;
            this.refutationSquare = refutationSquare;
            pointer = end = 0;

            this.stage = position.IsCheck() ? Stage.EvasionTT : stage;
            if (ttMove == Move.None || !position.IsPseudoLegal(ttMove))
                this.stage++;

            this.buffer = buffer;
        }

        //Constructor for main search
        public MovePicker(Position position, Move ttMove, ButterflyTable butterfly, Span<MoveScore> buffer) 
            : this(Stage.MainTT, position, ttMove, butterfly, Square.None, buffer) { }

        //Constructor for quiescence search
        public MovePicker(Position position, Move ttMove, ButterflyTable butterfly, Square refutationSquare, Span<MoveScore> buffer)
            : this(Stage.QSearchTT, position, ttMove, butterfly, refutationSquare, buffer) { }
        
        private Stage stage;

        private readonly Position position;
        private readonly Move ttMove;
        private readonly Square refutationSquare;
        private readonly ButterflyTable butterfly;

        private readonly Span<MoveScore> buffer;
        private int pointer;
        private int end;

        private int badCaptureCounter;

        public Move Current { get; private set; }
        public readonly MovePicker GetEnumerator() => this;

        public bool MoveNext()
        {
            while (true)
            {
                switch (stage)
                {
                    //Transposition tables
                    case Stage.MainTT:
                    case Stage.EvasionTT:
                    case Stage.QSearchTT:
                        stage++;
                        Current = ttMove;
                        return true;

                    //Normal Positions
                    case Stage.CaptureGenerate:
                    case Stage.QCaptureGenerate:
                        end += MoveGenerators.Capture.Generate(position, buffer).Length;
                        ScoreCaptures();
                        InsertionSort.Sort(buffer[pointer..end]);
                        stage++;
                        continue;
                    case Stage.GoodCaptures:
                        MoveScore move = FindNext();
                        if (!position.StaticExchangeEvaluationGE(move))
                        {
                            buffer[badCaptureCounter++] = move;
                            continue;
                        }
                        Current = move;
                        if (Current != Move.None)
                            return true;
                        stage++;
                        continue;
                    case Stage.QuietGenerate:
                        end += MoveGenerators.Quiet.Generate(position, buffer[end..]).Length;
                        ScoreQuiets();
                        InsertionSort.Sort(buffer[pointer..end]);
                        stage++;
                        continue;
                    case Stage.Quiet:
                        Current = FindNext();
                        if (Current != Move.None)
                            return true;
                        pointer = 0;
                        stage++;                       
                        continue;
                    case Stage.BadCaptures:
                        if (pointer < badCaptureCounter)
                        {
                            Current = FindNext();
                            return true;
                        }                       
                        return false;

                    case Stage.EvasionGenerate:
                        end += MoveGenerators.Evasion.Generate(position, buffer).Length;
                        ScoreEvasions();
                        InsertionSort.Sort(buffer[pointer..end]);
                        stage++;
                        continue;
                    case Stage.Evasions:
                        Current = FindNext();
                        return Current != Move.None;

                    case Stage.QCapture:
                        Current = FindNext();
                        return Current != Move.None;
                }
            }
        }

        private MoveScore FindNext()
        {
            while (pointer < end)
            {
                MoveScore move = buffer[pointer++];

                if (move != ttMove)
                    return move;
                else
                    continue;
            }

            return default;
        }

        private readonly void ScoreCaptures()
        {
            for (int i = pointer; i < end; i++)
            {
                Move move = buffer[i].Move;
                buffer[i].Score = Pieces.PieceValue(position.PieceAt(move.DestinationSquare()));                          
            }
        }

        private readonly void ScoreQuiets()
        {
            Color us = position.ActiveColor;
            Color enemy = us.Flip();

            Bitboard pawnAttacks = position.AttacksBy(enemy, PieceType.Pawn);
            Bitboard minorAttacks = position.AttacksBy(enemy, PieceType.Bishop) | position.AttacksBy(enemy, PieceType.Knight) | pawnAttacks;
            Bitboard rookAttacks = position.AttacksBy(enemy, PieceType.Rook) | minorAttacks;

            Bitboard piecesInDanger = 0;
            piecesInDanger |= position.Pieces(us, PieceType.Queen) | rookAttacks;
            piecesInDanger |= position.Pieces(us, PieceType.Rook) | minorAttacks;
            piecesInDanger |= position.Pieces(us, PieceType.Bishop, PieceType.Knight) | pawnAttacks;

            for (int i = pointer; i < end; i++)
            {
                ref var ptr = ref buffer[i];

                Piece piece = position.PieceAt(ptr.Move.OriginSquare());
                PieceType pieceType = piece.TypeOf();

                Square origin = ptr.Move.OriginSquare();
                Square destination = ptr.Move.DestinationSquare();

                int value = butterfly.Get((int)position.ActiveColor, ptr.Move.OriginDestination());

                value += !piecesInDanger.Contains(origin) ? 0
                                                          : pieceType switch
                                                          {
                                                              PieceType.Queen => 7500,
                                                              PieceType.Rook => 5000,
                                                              PieceType.Bishop or
                                                              PieceType.Knight => 2500,
                                                              _ => 0,
                                                          };

                value -= pieceType switch
                {
                    PieceType.Queen => !rookAttacks.Contains(destination)  ? 0
                                     : !minorAttacks.Contains(destination) ? 5000
                                     : !pawnAttacks.Contains(destination)  ? 10000
                                                                           : 15000,
                    PieceType.Rook => !minorAttacks.Contains(destination)  ? 0
                                    : !pawnAttacks.Contains(destination)   ? 5000
                                                                           : 10000,
                    PieceType.Bishop or
                    PieceType.Knight => !pawnAttacks.Contains(destination) ? 0
                                                                           : 5000,
                    _ => 0,
                };

                value += position.CheckSquares(pieceType).Contains(destination) ? 10000 : 0;

                ptr.Score = value;
            }
        }

        private readonly void ScoreEvasions()
        {
            //We want to search captures first after a check so we give them this 
            //huge bonus to move them up during sorting
            const int evasionCaptureBonus = 10000; 

            for (int i = pointer; i < end; i++)
            {
                Move move = buffer[i].Move;
                if (position.IsCapture(move))
                {
                    buffer[i].Score = evasionCaptureBonus
                                    - Pieces.PieceValue(position.PieceAt(move.OriginSquare()));
                }
                else
                {
                    buffer[i].Score = butterfly.Get((int)position.ActiveColor, move.OriginDestination());
                }
            }
        }
    }
}