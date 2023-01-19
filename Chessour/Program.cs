namespace Chessour
{
    class Program
    {
        public static void Main(string[] args)
        {
            Bitboards.Init();
            Zobrist.Init();
            PSQT.Init();
            Evaluation.Init();         
            Engine.Init();

            UCI.Run(args);

            Engine.Threads.SetSize(0);
        }
    }
}
