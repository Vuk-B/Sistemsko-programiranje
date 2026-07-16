using Akka.Actor;
using Akka.Configuration;

namespace WebServer
{
    class Program
    {
        static async Task Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var hocon = ConfigurationFactory.ParseString(@"
                akka {
                    stdout-loglevel = OFF
                    loglevel = INFO
                }
                ner-dispatcher {
                    type = Dispatcher
                    executor = ""fork-join-executor""
                    fork-join-executor {
                        parallelism-min = 2
                        parallelism-max = 4
                    }
                    throughput = 1
                }
            ");

            var system = ActorSystem.Create("youtube-ner-system", hocon);

            var youTubeService = new YouTubeService();
            var nerService = new NerService();

            var coordinator = system.ActorOf(
                CoordinatorActor.Props(youTubeService, nerService),
                "coordinator");

            youTubeService.Start(coordinator);

            var webServer = new WebServer(coordinator);

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Logger.Info("Primljen Ctrl+C, gasenje...");
                Shutdown(youTubeService, webServer, system).GetAwaiter().GetResult();
            };

            Logger.Info("YouTube NER server pokrenut. Pritisni Ctrl+C za gasenje.");
            await webServer.StartAsync();
        }

        static async Task Shutdown(YouTubeService yt, WebServer server, ActorSystem system)
        {
            yt.Dispose();
            server.Stop();
            await system.Terminate();
            Logger.Info("Aplikacija zatvorena.");
        }
    }
}
