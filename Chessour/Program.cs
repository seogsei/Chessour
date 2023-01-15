namespace Chessour
{
    class Program
    {        
        public static void Main(string[] args)
        {
            Bitboards.Init();
            Position.Init();
            PSQT.Init();
            Evaluation.Init();

            UCI.Run(args);

            Engine.Threads.SetSize(0);
        }
    }
}
