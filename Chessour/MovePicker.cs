using static Chessour.Evaluation;

namespace Chessour
{
    ref struct MovePicker
    {
        enum Stage
        {
            MainTT, CaptureGenerate, Capture, QuietGenerate, Quiet,
            EvasionTT, EvasionGenerate, Evasions,
            QSearchTT, QCaptureGenerate, QCapture
        }

        readonly Position position;
        readonly Move ttMove;
        readonly Span<MoveScore> moves;

        Stage stage;
        int curent;
        int end;

        public MovePicker(Position position, Move ttMove, Span<MoveScore> buffer)
        {
            moves = buffer;
            this.position = position;
            this.ttMove = ttMove;
            curent = end = 0;
            stage = position.IsCheck() ? Stage.EvasionTT : Stage.MainTT;

            if (ttMove == Move.None || !position.IsPseudoLegal(ttMove))
                stage++;
        }
        public MovePicker(Position position, Move ttMove, Square s, Span<MoveScore> buffer)
        {
            moves = buffer;
            this.position = position;
            this.ttMove = ttMove;
            curent = end = 0;
            stage = position.IsCheck() ? Stage.EvasionTT : Stage.QSearchTT;
            if (ttMove == Move.None || !position.IsPseudoLegal(ttMove))
                stage++;
        }

        public void Generate(GenerationType type)
        {
            end = MoveGenerator.Generate(type, position, moves, curent);
        }

        public Move NextMove()
        {
        top:
            switch (stage)
            {
                //Transposition tables
                case Stage.MainTT:
                case Stage.EvasionTT:
                case Stage.QSearchTT:
                    stage++;
                    return ttMove;

                //Normal Positions
                case Stage.CaptureGenerate:
                    curent = 0;
                    end = MoveGenerator.Generate(GenerationType.Captures, position, moves, curent);

                    Score(GenerationType.Captures);
                    PartialInsertionSort(curent, end);
                    stage++;
                    goto top;

                case Stage.Capture:
                    if (FindNext() != Move.None)
                        return moves[curent - 1].Move;
                    ++stage;
                    goto top;
                case Stage.QuietGenerate:
                    curent = 0;
                    end = MoveGenerator.Generate(GenerationType.Quiets, position, moves, curent);
                    stage++;
                    goto top;

                case Stage.Quiet:
                    return FindNext();

                //Evasions
                case Stage.EvasionGenerate:
                    Generate(GenerationType.Evasions);
                    stage++;
                    goto top;
                case Stage.Evasions:
                    return FindNext();

                //QSearch
                case Stage.QCaptureGenerate:
                    Generate(GenerationType.Captures);
                    Score(GenerationType.Captures);
                    PartialInsertionSort(curent, end);
                    stage++;
                    goto top;
                case Stage.QCapture:
                    return FindNext();
            }

            Debug.Assert(false);
            return Move.None;
        }
        public void Score(GenerationType type)
        {
            int i = curent;
            for (MoveScore m = moves[i]; i < end; m = moves[++i])
                if (type == GenerationType.Captures)
                {
                    m.Score = (int)PieceValue(GamePhase.MidGame, position.PieceAt(m.Move.ToSquare()))
                            - (int)PieceValue(GamePhase.MidGame, position.PieceAt(m.Move.FromSquare()));

                    if (m.Move.TypeOf() == MoveType.Promotion)
                        m.Score += (int)PieceValue(GamePhase.MidGame, (Piece)m.Move.PromotionPiece());
                }
        }
        private Move FindNext()
        {
            while (curent < end)
            {
                if (moves[curent] != ttMove)
                    return moves[curent++];
                curent++;
            }
            return Move.None;
        }
        private void PartialInsertionSort(int start, int end)
        {
            for (int i = start; i < end; ++i)
            {
                MoveScore m = moves[i];
                int j = i - 1;

                // Move elements of arr[0..i-1],
                // that are greater than key,
                // to one position ahead of
                // their current position
                while (j >= 0 && moves[j] > m)
                {
                    moves[j + 1] = moves[j];
                    j--;
                }
                moves[j + 1] = m;
            }
        }
    }
}
