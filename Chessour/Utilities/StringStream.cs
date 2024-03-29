﻿namespace Chessour.Utilities
{
    internal ref struct StringStream
    {
        private readonly ReadOnlySpan<char> str;
        private int position;

        public StringStream(string str) : this(str.AsSpan())
        {

        }
        public StringStream(ReadOnlySpan<char> str)
        {
            this.str = str;
            position = 0;
        }

        public void SkipWhiteSpace()
        {
            while (position < str.Length && str[position] == ' ') position++;
        }

        public bool Extract(out string result)
        {
            result = NextToken().ToString();

            return result.Length > 0;
        }

        public bool Extract(out int result)
        {
            return int.TryParse(NextToken(), out result);
        }

        public bool Extract(out long result)
        {
            return long.TryParse(NextToken(), out result);
        }

        public bool Extract(out char result)
        {
            result = default;
            if (position >= str.Length - 1)
                return false;

            result = str[position++];
            return true;
        }

        public long ReadInt64()
        {
            Extract(out long result);
            return result;
        }

        public string ReadUntil(string limiter)
        {
            if (position >= str.Length)
                return string.Empty;

            int startIdx = position;

            while (Extract(out string token))
                if (token == limiter)
                    break;

            return str[startIdx..(position - 1)].ToString();
        }

        private ReadOnlySpan<char> NextToken()
        {
            if (position >= str.Length)
                return ReadOnlySpan<char>.Empty;

            int start = position;

            while (position < str.Length && str[position] != ' ') position++;

            return str[start..position++];
        }
    }
}
