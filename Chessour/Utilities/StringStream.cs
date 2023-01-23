namespace Chessour.Utilities
{
    ref struct StringReader
    {
        readonly ReadOnlySpan<char> str;
        int position;

        public StringReader(string str) : this(str.AsSpan())
        {

        }
        public StringReader(ReadOnlySpan<char> str)
        {
            this.str = str;
            position = -1;
        }

        public void SkipWhiteSpace()
        {
            while (position < str.Length && str[position] == ' ')
                position++;
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
        public bool Extract(out Depth result)
        {
            bool r = Extract(out int r2);
            result = (Depth)r2;
            return r;
        }
        public bool Extract(out char result)
        {
            result = (char)0; 
            
            if (position >= str.Length - 1)
                return false;

            result = str[++position];
            return true;
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

            int start = ++position;

            while (++position < str.Length && str[position] != ' ') { }

            return str[start..position];
        }
    }
}
