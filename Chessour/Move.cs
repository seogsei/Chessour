namespace Chessour
{
    public enum MoveType
    {
        Quiet,
        Promotion = 1 << 12,
        EnPassant = 2 << 12,
        Castling = 3 << 12,
        Mask = 3 << 12
    }

    public enum Move
    {
        None = 0,
        Null = 65,
    }

    /// <summary>
    /// Single field structs are slower compared to enums in C# so for performance reasons we use an enum with extension methods
    /// </summary>
    public static class MoveExtensions
    {
        public static Move CreateMove(Square origin, Square destination)
        {
            return (Move)(((int)destination << 6) | (int)origin);
        }
        public static Move CreateCastlingMove(Square origin, Square destination)
        {
            return (Move)((int)MoveType.Castling | ((int)destination << 6) | (int)origin);
        }
        public static Move CreateEnPassantMove(Square origin, Square destination)
        {
            return (Move)((int)MoveType.EnPassant | ((int)destination << 6) | (int)origin);
        }
        public static Move CreatePromotionMove(Square origin, Square destination, PieceType promotionPiece)
        {
            Debug.Assert(promotionPiece >= PieceType.Knight && promotionPiece <= PieceType.Queen);

            return (Move)(((promotionPiece - PieceType.Knight) << 14) | (int)MoveType.Promotion | ((int)destination << 6) | (int)origin);
        }
        public static Square OriginSquare(this Move move)
        {
            return (Square)move & Square.h8;
        }
        public static Square DestinationSquare(this Move move)
        {
            return (Square)((int)move >> 6) & Square.h8;
        }
        public static MoveType Type(this Move move)
        {
            return (MoveType)move & MoveType.Mask;
        }
        public static PieceType PromotionPiece(this Move move)
        {
            return ((int)move >> 14) + PieceType.Knight;
        }
        public static string DebuggerDisplay(this Move move)
        {
            try
            {
                return UCI.Move(move);
            }
            catch
            {
                return "Invalid move: " + move;
            }
        }
    }

    /*
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct MoveStruct :
         IEqualityOperators<MoveStruct, MoveStruct, bool>
    {
        private readonly int value;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public MoveStruct(Square from, Square to)
        {
            value = ((int)to << 6) | (int)from;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public MoveStruct(Square from, Square to, MoveType type)
        {
            Debug.Assert(type != MoveType.Promotion);

            value = (int)type | ((int)to << 6) | (int)from;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public MoveStruct(Square from, Square to, PieceType promotionPiece)
        {
            Debug.Assert(promotionPiece >= PieceType.Knight && promotionPiece <= PieceType.Queen);

            value = ((promotionPiece - PieceType.Knight) << 14) | (int)MoveType.Promotion | ((int)to << 6) | (int)from;
        }

        public readonly Square From 
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => (Square)value & Square.h8;
        }
        public readonly Square To 
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => (Square)(value >> 6) & Square.h8;
        }
        public readonly MoveType Type 
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => (MoveType)value & MoveType.Castling;
        }
        public readonly PieceType PromotionPiece 
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => PieceType.Knight + ((value >> 14) & 3);
        }

        public static MoveStruct None { get; } = default;

        public static MoveStruct Null { get; } = new(Square.a2, Square.a2);

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is not MoveStruct)
                return false;
            return value == ((MoveStruct)obj).value;
        }

        public override int GetHashCode()
        {
            return value;
        }

        public override string ToString()
        {
            if (this == None)
                return "(none)";

            if (this == Null)
                return "0000";

            Square from = this.From;
            Square to = this.To;

            if (this.Type == MoveType.Castling)
                to = BoardRepresentation.MakeSquare(to > from ? File.g : File.c, from.GetRank());

            string moveString = string.Concat(from, to);
            if (this.Type == MoveType.Promotion)
                moveString += " pnbrqk"[(int)this.PromotionPiece];

            return moveString;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(MoveStruct left, MoveStruct right) => left.value == right.value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(MoveStruct left, MoveStruct right) => left.value != right.value;
    }
    */
}
