namespace Chessour.Types
{
    public enum MoveType
    {
        Quiet,
        Promotion = 1 << 14,
        EnPassant = 2 << 14,
        Castling = 3 << 14
    }

    public readonly struct MoveStruct
    {
        public int Value { get; }

        public Square From{ get => (Square)((Value >> 6) & 0x3f); }
        public Square To{ get => (Square)((Value >> 6)); }
        public MoveType Tyoe { get => (MoveType)(Value & (3 << 14)); }

        public MoveStruct(Square from, Square to, MoveType type = MoveType.Quiet, PieceType promotion = PieceType.Knight)
        {
            Value = (int)type | ((int)(promotion - 2) << 12) | ((int)from << 6) | (int)to;
        }
    }
    
    public enum Move
    {
        None,
        Null = 65
    }

    public static partial class Core
    {
        public static bool IsValid(Move move)
        {
            return move.FromSquare() != move.ToSquare();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Move MakeMove(Square from, Square to, MoveType type = MoveType.Quiet, PieceType promotion = PieceType.Knight)
        {
            return (Move)((int)type | ((int)(promotion - 2) << 12) | ((int)from << 6) | (int)to);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square FromSquare(this Move move)
        {
            return (Square)(((int)move >> 6) & 0x3F);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square ToSquare(this Move move)
        {
            return (Square)((int)move & 0x3F);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MoveType TypeOf(this Move move)
        {
            return (MoveType)((int)move & (3 << 14));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PieceType PromotionPiece(this Move move)
        {
            return (PieceType)(((int)move >> 12) & 3) + 2;
        }
    }
    
}