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

        public MovePicker(Position position, Move ttMove, Span<MoveScore> buffer)
        {
            this.buffer = buffer;
            this.position = position;
            this.ttMove = ttMove;
            this.refutationSquare = Square.None;
            pointer = end = 0;
            stage = position.IsCheck() ? Stage.EvasionTT : Stage.MainTT;

            if (ttMove == Move.None || !position.IsPseudoLegal(ttMove))
                stage++;
        }

        public MovePicker(Position position, Move ttMove, Square refutationSquare, Span<MoveScore> buffer)
        {
            this.buffer = buffer;
            this.position = position;
            this.ttMove = ttMove;
            this.refutationSquare = refutationSquare;
            pointer = end = 0;
            stage = position.IsCheck() ? Stage.EvasionTT : Stage.QSearchTT;
            if (ttMove == Move.None || !position.IsPseudoLegal(ttMove))
                stage++;
        }

        private readonly Span<MoveScore> buffer;
        private int pointer;
        private int end;
        private Stage stage;

        private readonly Position position;
        private readonly Move ttMove;
        private readonly Square refutationSquare;

        public Move Current { get; private set; }
        public MovePicker GetEnumerator() => this;

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
                        break;
                    case Stage.GoodCaptures:
                        Current = FindNext();
                        if (Current != Move.None)
                            return true;
                        stage++;
                        break;
                    case Stage.QuietGenerate:
                        end += MoveGenerators.Quiet.Generate(position, buffer[end..]).Length;
                        ScoreQuiets();
                        InsertionSort.Sort(buffer[pointer..end]);
                        stage++;
                        break;
                    case Stage.Quiet:
                        Current = FindNext();
                        if (Current != Move.None)
                            return true;
                        stage++;
                        break;
                    case Stage.BadCaptures:
                        Current = FindNext();
                        if (Current != Move.None)
                            return true;
                        return false;

                    case Stage.EvasionGenerate:
                        end += MoveGenerators.Evasion.Generate(position, buffer).Length;
                        ScoreEvasions();
                        InsertionSort.Sort(buffer[pointer..end]);
                        stage++;
                        break;
                    case Stage.Evasions:
                        Current = FindNext();
                        return Current != Move.None;

                    case Stage.QCapture:
                        Current = FindNext();
                        return Current != Move.None;
                }
            }
        }

        private Move FindNext()
        {
            while (pointer < end)
            {
                Move move = buffer[pointer++].Move;

                if (move != ttMove)
                    return move;
                else
                    continue;
            }

            return Move.None;
        }

        private void ScoreCaptures()
        {
            for (int i = pointer; i < end; i++)
            {
                Move move = buffer[i].Move;
                buffer[i].Score = Pieces.PieceValue(position.PieceAt(move.DestinationSquare()));                              
            }
        }

        private void ScoreQuiets()
        {
            for (int i = pointer; i < end; i++)
            {
                Move move = buffer[i].Move;
                Piece piece = position.PieceAt(move.OriginSquare());
                buffer[i].Score = move.Type() == MoveType.Promotion ? Pieces.QueenValue
                                                                    : PSQT.Get(piece, move.DestinationSquare()).MidGame
                                                                    - PSQT.Get(piece, move.OriginSquare()).MidGame;
            }
        }

        private void ScoreEvasions()
        {
            //We want to search captures first after a check so we give them this 
            //huge bonus to move them up during sorting
            const int evasionCaptureBonus = 4 * Pieces.QueenValue; 

            for (int i = pointer; i < end; i++)
            {
                Move move = buffer[i].Move;
                if (position.IsCapture(move))
                {
                    buffer[i].Score = Pieces.PieceValue(position.PieceAt(move.DestinationSquare())) + (evasionCaptureBonus);
                }
                else
                {
                    Piece piece = position.PieceAt(move.OriginSquare());
                    buffer[i].Score = move.Type() == MoveType.Promotion ? Pieces.QueenValue
                                                                        : PSQT.Get(piece, move.DestinationSquare()).MidGame
                                                                        - PSQT.Get(piece, move.OriginSquare()).MidGame;
                }
            }
        }
    }
}