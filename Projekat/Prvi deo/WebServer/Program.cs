namespace WebServer
{
    class Program
    {
        static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var server = new HttpServer(5050, Environment.ProcessorCount * 2);

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                server.Stop();
            };

            server.Start();
            Logger.Info("Aplikacija zatvorena.");
        }
    }
}
