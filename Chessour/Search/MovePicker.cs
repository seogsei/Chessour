using System.Collections;

namespace Chessour.Search;

internal ref struct MovePicker
{
    private readonly Position position;
    private readonly Move ttMove;
    private readonly Span<MoveScore> buffer;
    private int curent;
    private int end;
    private Stage stage;

    public Move Current { get; private set; }

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
                    end = MoveGenerator.GenerateCaptures(position, buffer[curent..]).Length;
                    Score(GenerationType.Captures);
                    PartialInsertionSort(curent, end);
                    stage++;
                    break;
                case Stage.GoodCaptures:
                    Current = FindNext();
                    if (Current != Move.None)
                        return true;
                    ++stage;
                    break;
                case Stage.QuietGenerate:
                    end = MoveGenerator.GenerateQuiets(position, buffer[curent..]).Length;
                    stage++;
                    break;
                case Stage.Quiet:
                    Current = FindNext();
                    if(Current != Move.None)
                        return true;
                    ++stage;
                    break;
                case Stage.BadCaptures:
                    Current = FindNext();
                    return Current != Move.None;

                case Stage.EvasionGenerate:
                    end = MoveGenerator.GenerateEvasions(position, buffer[curent..]).Length;
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

    public MovePicker GetEnumerator() 
    {
        return this;
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
                end = MoveGenerator.GenerateCaptures(position, buffer[curent..]).Length;
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
                end = MoveGenerator.GenerateQuiets(position, buffer[curent..]).Length;
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
                end = MoveGenerator.GenerateEvasions(position, buffer[curent..]).Length;
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

    public void Score(GenerationType type)
    {

    }

    private Move FindNext()
    {
        for (; curent < end; curent++)
            if (buffer[curent].Move != ttMove)
                return buffer[curent++].Move;
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
