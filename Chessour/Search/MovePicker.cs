using Chessour.Evaluation;
using Chessour.Utilities;
using static Chessour.GenerationTypes;

namespace Chessour.Search
{
    internal ref struct MovePicker
    {
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

        private enum Stage
        {
            MainTT, CaptureGenerate, GoodCaptures, QuietGenerate, Quiet, BadCaptures,
            EvasionTT, EvasionGenerate, Evasions,
            QSearchTT, QCaptureGenerate, QCapture
        }

        private readonly Span<MoveScore> buffer;
        private readonly Position position;
        private readonly Move ttMove;
        private readonly Square refutationSquare;

        private int pointer;
        private int end;
        private Stage stage;

        public Move Current { get; private set; }

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
                        end = pointer + MoveGenerators.Capture.Generate(position, buffer[pointer..]).Length;
                        Score(Captures);
                        InsertionSort.PartialSort(buffer, pointer, end);
                        stage++;
                        break;
                    case Stage.GoodCaptures:
                        Current = FindNext();
                        if (Current != Move.None)
                            return true;
                        stage++;
                        break;
                    case Stage.QuietGenerate:
                        end = pointer + MoveGenerators.Quiet.Generate(position, buffer[pointer..]).Length;
                        Score(Quiets);
                        InsertionSort.PartialSort(buffer, pointer, end);
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
                        end = pointer + MoveGenerators.Evasion.Generate(position, buffer[pointer..]).Length;
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

        public void Score(GenerationTypes type)
        {
            switch (type)
            {
                case Captures:
                    for (int i = pointer; i < end; i++)
                    {
                        Move move = buffer[i].Move;
                        buffer[i].Score = Pieces.PieceValue(position.PieceAt(move.DestinationSquare()))
                                        - Pieces.PieceValue(position.PieceAt(move.OriginSquare()));
                    }
                    return;
                case Quiets:
                    for (int i = pointer; i < end; i++)
                    {
                        Move move = buffer[i].Move;
                        Piece piece = position.PieceAt(move.OriginSquare());
                        buffer[i].Score = PSQT.Get(piece, move.DestinationSquare()).MidGame
                                        - PSQT.Get(piece, move.OriginSquare()).MidGame
                                        + move.Type() == MoveType.Promotion ? Pieces.QueenValue : 0;
                    }
                    return;
            }
        }

        private Move FindNext()
        {
            while (pointer < end)
            {
                Move move = buffer[pointer++].Move;

                if (move != ttMove)
                    return move;
            }

            return Move.None;
        }

        public MovePicker GetEnumerator() => this;
    }
}