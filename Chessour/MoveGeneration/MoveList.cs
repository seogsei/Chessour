using Chessour.Search;

namespace Chessour.MoveGeneration;

internal ref struct MoveList
{
    private readonly Span<MoveScore> moves;
    public int Count { get; private set; }

    public MoveList(Span<MoveScore> buffer)
    {
        Debug.Assert(buffer.Length == MoveGenerator.MAX_MOVE_COUNT);

        moves = buffer;
        Count = 0;
    }

    public MoveList(Position position, Span<MoveScore> buffer) : this(buffer)
    {
        Generate(position);
    }

    public MoveList(GenerationType type, Position position, Span<MoveScore> buffer) : this(buffer)
    {
        Generate(type, position);
    }

    public void Generate(Position position)
    {
        Count = MoveGenerator.Generate(position, moves, Count);
    }

    public void Generate(GenerationType type, Position position)
    {
        Count = MoveGenerator.Generate(type, position, moves, Count);
    }

    public bool Contains(Move m)
    {
        foreach (Move move in this)
            if (move == m)
                return true;
        return false;
    }

    public Enumerator GetEnumerator()
    {
        return new(this);
    }
    public ref struct Enumerator
    {
        private MoveList moveList;
        private int idx;

        public MoveScore Current => moveList.moves[idx];

        public Enumerator(MoveList moveList)
        {
            this.moveList = moveList;
            idx = -1;
        }

        public bool MoveNext()
        {
            return ++idx < moveList.Count;
        }
    }
}
