using Chessour.Evaluation;
using Chessour.MoveGeneration;

namespace Chessour.Search;

internal ref struct MovePicker
{
    private readonly Position position;
    private readonly Move ttMove;
    private readonly Span<MoveScore> buffer;
    private int curent;
    private int end;
    private Stage stage;

    public MovePicker(Position position, Move ttMove, Span<MoveScore> buffer)
    {
        this.buffer = buffer;
        this.position = position;
        this.ttMove = ttMove;
        curent = end = 0;
        stage = position.IsCheck() ? Stage.EvasionTT : Stage.MainTT;

        if (ttMove == Move.None || !position.IsPseudoLegal(ttMove))
            stage++;
    }

    public MovePicker(Position position, Move ttMove, Square s, Span<MoveScore> buffer)
    {
        this.buffer = buffer;
        this.position = position;
        this.ttMove = ttMove;
        curent = end = 0;
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
            case Stage.QCaptureGenerate:
                Generate(GenerationType.Captures);
                Score(GenerationType.Captures);
                PartialInsertionSort(curent, end);
                stage++;
                goto top;

            case Stage.GoodCaptures:
                if (FindNext() != Move.None)
                    return buffer[curent - 1];
                ++stage;
                goto top;
            case Stage.QuietGenerate:
                Generate(GenerationType.Quiets);
                stage++;
                goto top;
            case Stage.Quiet:
                if (FindNext() != Move.None)
                    return buffer[curent - 1];
                ++stage;
                goto top;
            case Stage.BadCaptures:
                return FindNext();

            case Stage.EvasionGenerate:
                Generate(GenerationType.Evasions);
                stage++;
                goto top;
            case Stage.Evasions:
                return FindNext();

            case Stage.QCapture:
                return FindNext();
        }

        Debug.Assert(false);
        return Move.None;
    }

    public void Generate(GenerationType type)
    {
        end = MoveGenerator.Generate(type, position, buffer, curent);
    }

    public void Score(GenerationType type)
    {
        for (int i = curent; i < end; i++)
        {
            MoveScore m = buffer[i];
            if (type == GenerationType.Captures)
            {
                m.Score = Pieces.PieceScore(position.PieceAt(m.Move.To)).MidGame
                        - Pieces.PieceScore(position.PieceAt(m.Move.From)).MidGame;
            }
        }
    }

    private Move FindNext()
    {
        for (; curent < end; curent++)
            if (buffer[curent] != ttMove)
                return buffer[curent++];
        return Move.None;
    }

    private void PartialInsertionSort(int start, int end)
    {
        for (int i = start; i < end; ++i)
        {
            MoveScore m = buffer[i];
            int j = i - 1;

            // Move elements of arr[0..i-1],
            // that are greater than key,
            // to one position ahead of
            // their current position
            while (j >= 0 && buffer[j] > m)
            {
                buffer[j + 1] = buffer[j];
                j--;
            }
            buffer[j + 1] = m;
        }
    }
}
