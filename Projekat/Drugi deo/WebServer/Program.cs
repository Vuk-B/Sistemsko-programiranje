namespace WebServer
{
    class Program
    {
        static async Task Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var server = new HttpServer(5050, 100);
            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Logger.Info("Primljen Ctrl+C, gasenje...");
                cts.Cancel();
            };

            await server.StartAsync(cts.Token);

            Logger.Info("Aplikacija zatvorena.");
        }
    }
}
