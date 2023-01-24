namespace Chessour
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            UCI.Loop(args);

            Engine.Threads.SetSize(0);
        }
    }
}
