﻿using Chessour.Types;
using System;
using System.Runtime.InteropServices;

namespace Chessour
{
    public enum Bound
    {
        Exact,
        UpperBound,
        LowerBound
    }

    struct TTEntry
    {
        Key key; 
        byte depth; 
        byte gen8; 
        ushort move; 
        short eval;

        public Key Key { get => key; }
        public int Depth { get => depth; }
        public Move Move { get => (Move)move; }
        public bool isPV { get => (gen8 & 4) != 0; }
        public Bound Bound { get => (Bound)(gen8 & 3); }
        public Value Evaluation { get => (Value)eval; }

        public void Save(Key key, int depth, Move move, bool isPV, Bound boundType, Value evaluation)
        {
            this.key = key;
            this.move = (ushort)move;
            this.eval = (short)evaluation;
            this.depth = (byte)depth;

            byte gen8 = (byte)((isPV ? 4 : 0) | (int)boundType);
            this.gen8 = gen8;
        }
    }

    static class TranspositionTable
    {
        static TTEntry[] entries;
        static int shift;

        static TranspositionTable()
        {
            entries = new TTEntry[1 << 16];
            shift = 64 - 16;
        }

        public static void Resize(int sizeInMB)
        {
            if (sizeInMB < 0)
                return;

            int entryCount = sizeInMB * 1024 * 1024 / Marshal.SizeOf(typeof(TTEntry));
        }
        public static void Clear()
        {
            Array.Clear(entries);
        }

        public static ref TTEntry ProbeTT(Key key, out bool found)
        {
            ref TTEntry entry = ref Find(key);

            found = entry.Depth != 0;
            return ref entry;
        }

        private static ref TTEntry Find(Key key)
        {
            return ref entries[(ulong)key >> shift];
        }
    }
}
