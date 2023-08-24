using System.Collections;

namespace Chessour.Search;

internal ref struct MovePicker
{
    private readonly Span<MoveScore> buffer;
    private readonly Position position;
    private readonly Move ttMove;
    private readonly Square refutationSquare;
    private int pointer;
    private int end;
    private Stage stage;

    public Move Current { get; private set; }

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
                    end = pointer + MoveGenerator.GenerateCaptures(position, buffer[pointer..]).Length;
                    Score(GenerationType.Captures);
                    Sort(pointer, end);
                    stage++;
                    break;
                case Stage.GoodCaptures:
                    Current = FindNext();
                    if (Current != Move.None)
                        return true;
                    stage++;
                    break;
                case Stage.QuietGenerate:
                    end = pointer + MoveGenerator.GenerateQuiets(position, buffer[pointer..]).Length;
                    Score(GenerationType.Quiets);
                    Sort(pointer, end);
                    stage++;
                    break;
                case Stage.Quiet:
                    Current = FindNext();
                    if(Current != Move.None)
                        return true;
                    stage++;
                    break;
                case Stage.BadCaptures:
                    Current = FindNext();
                    if (Current != Move.None)
                        return true;
                    return false;

                case Stage.EvasionGenerate:
                    end = pointer + MoveGenerator.GenerateEvasions(position, buffer[pointer..]).Length;
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

    public void Score(GenerationType type)
    {
        switch (type)
        {
            case GenerationType.Captures:
                for(int i = pointer; i< end; i++)
                {
                    Move move = buffer[i].Move;
                    buffer[i].Score = Evaluation.Pieces.PieceValue(position.PieceAt(move.DestinationSquare()))
                                    - Evaluation.Pieces.PieceValue(position.PieceAt(move.OriginSquare()));
                }
                return;
            case GenerationType.Quiets:
                for (int i = pointer; i < end; i++)
                {
                    Move move = buffer[i].Move;
                    Piece piece = position.PieceAt(move.OriginSquare());
                    buffer[i].Score = Evaluation.PSQT.Get(piece, move.DestinationSquare()).MidGame
                                    - Evaluation.PSQT.Get(piece, move.OriginSquare()).MidGame
                                    + move.Type() == MoveType.Promotion ? Evaluation.Pieces.PieceValue(PieceType.Queen) : 0;
                }
                return;
        }
    }

    private Move FindNext()
    {
        while(pointer < end)
        {
            Move move = buffer[pointer++].Move;

            if (move != ttMove)
                return move;
        }

        return Move.None;
    }

    private void Sort(int start, int end)
    {
        for (int i = start + 1; i < end; i++)
        {
            var move = buffer[i];

            int j = i - 1;
            // Move elements of arr[0..i-1],
            // that are greater than key,
            // to one position ahead of
            // their current position
            for (; j >= start && buffer[j] < move; j--)
                buffer[j + 1] = buffer[j];
                
            buffer[j + 1] = move;
        }
    }
}
