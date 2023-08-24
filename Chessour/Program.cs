namespace Chessour
{
    internal class Program
    {
        public static void Main(string[] args)
        {


            UCI uci = new();
            uci.Run(args);
        }
    }
}
