namespace Chessour;

public enum MoveType
{
    Quiet,
    Promotion = 1 << 14,
    EnPassant = 2 << 14,
    Castling = 3 << 14
}

public readonly struct Move
{
    public Move(int value)
    {
        Value = (ushort)value;
    }

    public Move(Square from, Square to)
    {
        Value = (ushort)(((int)from << 6) + (int)to);
    }

    public Move(Square from, Square to, MoveType moveType)
    {
        Value = (ushort)((int)moveType + ((int)from << 6) + (int)to);
    }

    public Move(Square from, Square to, PieceType promotionPiece)
    {
        Value = (ushort)((int)MoveType.Promotion + ((int)promotionPiece - 2 << 12) + ((int)from << 6) + (int)to);
    }

    public int Value { get; init; }

    public Square From
    {
        get
        {
            return (Square)(Value >> 6 & 0x3f);
        }
    }

    public Square To
    {
        get
        {
            return (Square)(Value & 0x3f);
        }
    }

    public MoveType Type
    {
        get
        {
            return (MoveType)Value & MoveType.Castling;
        }
    }

    public PieceType PromotionPiece
    {
        get
        {
            return (PieceType)((Value >> 12 & 0x3) + 2);
        }
    }

    public static Move None
    {
        get
        {
            return default;
        }
    }

    public static Move Null
    {
        get
        {
            return new(Square.a2, Square.a2);
        }
    }

    public static bool operator ==(Move left, Move right)
    {
        return left.Value == right.Value;
    }

    public static bool operator !=(Move left, Move right)
    {
        return left.Value != right.Value;
    }

    public override bool Equals(object? obj)
    {
        return Value.Equals(obj);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override string ToString()
    {
        if (this == None)
            return "(none)";

        if (this == Null)
            return "0000";

        Square from = From;
        Square to = To;

        if (Type == MoveType.Castling)
            to = MakeSquare(to > from ? File.g : File.c, from.GetRank());

        string moveString = string.Concat(from, to);
        if (Type == MoveType.Promotion)
            moveString += " pnbrqk"[(int)PromotionPiece];

        return moveString;
    }
}
