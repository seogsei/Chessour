using System.Numerics;

namespace Chessour
{   
    public enum MoveType
    {
        Quiet,
        Promotion = 1 << 12,
        EnPassant = 2 << 12,
        Castling = 3 << 12,
    }
    
    public readonly struct Move :
         IEqualityOperators<Move, Move, bool>
    {
        public int Value { get; }

        public static readonly Move None = default;

        public static readonly Move Null = new(Square.b1, Square.b1);

        internal Move(int value)
        {
            Value = value;
        }

        public Move(Square from, Square to)
        {
            Value = ((int)to << 6) | (int)from;
        }

        public Move(Square from, Square to, MoveType type)
        {
            Debug.Assert(type != MoveType.Promotion);

            Value = (int)type | ((int)to << 6) | (int)from;
        }

        public Move(Square from, Square to, PieceType promotionPiece)
        {
            Debug.Assert(promotionPiece >= PieceType.Knight && promotionPiece <= PieceType.Queen);

            Value = ((promotionPiece - PieceType.Knight) << 14) | (int)MoveType.Promotion | ((int)to << 6) | (int)from;
        }

        public readonly Square OriginSquare()
        {
            return (Square)Value & Square.h8;
        }

        public readonly Square DestinationSquare()
        {
            return (Square)(Value >> 6) & Square.h8;
        }

        public readonly int OriginDestination()
        {
            return Value & 0xfff;
        }

        public readonly MoveType Type()
        {
            return (MoveType)Value & MoveType.Castling;
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

            Square from = this.OriginSquare();
            Square to = this.DestinationSquare();

            if (this.Type() == MoveType.Castling)
                to = SquareExtensions.MakeSquare(to > from ? File.g : File.c, from.GetRank());

            string moveString = string.Concat(from, to);
            if (this.Type() == MoveType.Promotion)
                moveString += " pnbrqk"[(int)this.PromotionPiece()];

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
