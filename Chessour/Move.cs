namespace Chessour
{   
    public enum MoveType
    {
        Quiet,
        Promotion = 1 << 12,
        EnPassant = 2 << 12,
        Castling = 3 << 12,
    }
    
    public readonly struct Move
    {
        public int Value { get; }

        public static Move None { get; } = new();

        public static Move Null { get; } = new(Square.b1, Square.b1);

        internal Move(int value)
        {
            Value = value;
        }

        public Move(Square origin, Square destination)
            : this(((int)destination << 6) | (int)origin)
        {
           
        }

        public Move(Square origin, Square destination, MoveType moveType)
            : this (origin, destination)
        {
            Debug.Assert(moveType != Chessour.MoveType.Promotion);
            Value |= (int)moveType;
        }

        public Move(Square origin, Square destination, PieceType promotionPiece)
            : this(origin, destination)
        {
            Debug.Assert(promotionPiece >= PieceType.Knight && promotionPiece <= PieceType.Queen);
            Value |= ((promotionPiece - PieceType.Knight) << 14) | (int)Chessour.MoveType.Promotion;           
        }

        public readonly Square Origin()
        {
            return (Square)Value & Square.h8;
        }

        public readonly Square Destination()
        {
            return (Square)(Value >> 6) & Square.h8;
        }

        public readonly int OriginDestination()
        {
            return Value & 0xfff;
        }

        public readonly MoveType MoveType()
        {
            return (MoveType)Value & Chessour.MoveType.Castling;
        }

        public readonly PieceType PromotionPiece()
        {
            return PieceType.Knight + ((Value >> 14) & 3);
        }

        public override string ToString()
        {
            if (this == None)
                return "(none)";

            if (this == Null)
                return "0000";

            Square from = Origin();
            Square to = Destination();

            if (MoveType() == Chessour.MoveType.Castling)
                to = SquareExtensions.MakeSquare(to > from ? File.g : File.c, from.GetRank());

            string moveString = string.Concat(from, to);
            if (MoveType() == Chessour.MoveType.Promotion)
                moveString += " pnbrqk"[(int)PromotionPiece()];

            return moveString;
        }

        public static bool operator ==(Move left, Move right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(Move left, Move right)
        {
            return left.Value != right.Value;
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is Move move &&
                   this == move;
        }
    }
}
