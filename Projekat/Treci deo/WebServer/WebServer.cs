using System.Net;
using System.Text;
using System.Text.Json;
using Akka.Actor;

namespace WebServer
{
    public class WebServer
    {
        private readonly HttpListener _listener;
        private readonly IActorRef _coordinator;
        private volatile bool _running;

        public WebServer(IActorRef coordinator)
        {
            _coordinator = coordinator;
            _listener = new HttpListener();
            _listener.Prefixes.Add(Config.ServerPrefix);
        }

        public async Task StartAsync()
        {
            _running = true;
            _listener.Start();

            Logger.Info($"Web server slusa na {Config.ServerPrefix}");
            Logger.Info($"Primer: {Config.ServerPrefix}ner?videoId=abc123");

            while (_running)
            {
                try
                {
                    var context = await _listener.GetContextAsync();

                    _ = HandleAsync(context);
                }
                catch (HttpListenerException) when (!_running)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_running)
                        Logger.Error($"Greska pri prihvatanju konekcije: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            _listener.Stop();
            _listener.Close();
            Logger.Info("Web server zaustavljen.");
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            var request = context.Request;
            Logger.Info($"Pristigao HTTP zahtev: {request.HttpMethod} {request.Url}");

            try
            {
                var path = request.Url!.AbsolutePath.TrimStart('/').ToLowerInvariant();

                if (path == "favicon.ico")
                {
                    context.Response.StatusCode = 204;
                    context.Response.Close();
                    return;
                }

                if (path == "" || path == "/")
                {
                    await SendText(context, 200,
                        "YouTube NER server - Named Entity Recognition za komentare.\n" +
                        "Upotreba: /ner?videoId=<YOUTUBE_VIDEO_ID>\n" +
                        "Primer: /ner?videoId=abc123");
                    return;
                }

                if (path != "ner")
                {
                    await SendText(context, 404, "Nepoznata putanja. Probaj: /ner?videoId=<ID>");
                    return;
                }

                var videoId = request.QueryString["videoId"];
                if (string.IsNullOrWhiteSpace(videoId))
                {
                    await SendText(context, 400, "Nedostaje parametar 'videoId'. Primer: /ner?videoId=abc123");
                    return;
                }

                var result = await _coordinator.Ask<INerResponse>(
                    new GetNerResult(videoId),
                    Config.AskTimeout);

                switch (result)
                {
                    case NerSuccess ok:
                        Logger.Info($"Vraceno {ok.Entities.Count} entiteta za video {videoId}.");
                        await SendJson(context, 200, ok);
                        break;

                    case NerNotReady:
                        Logger.Info($"NER nije gotov za {videoId}, vracen 202.");
                        await SendJson(context, 202,
                            new { videoId, status = "pending", message = "NER obrada u toku. Pokusajte ponovo." });
                        break;

                    case NerError err:
                        Logger.Error($"NER greska za {videoId}: {err.Message}");
                        await SendJson(context, 500,
                            new { videoId, status = "error", message = err.Message });
                        break;
                }
            }
            catch (TaskCanceledException)
            {
                Logger.Warning("Ask timeout - aktor nije odgovorio na vreme.");
                await SendJson(context, 504,
                    new { status = "timeout", message = "Obrada traje predugo, pokusajte ponovo kasnije." });
            }
            catch (Exception ex)
            {
                Logger.Error($"Greska pri obradi HTTP zahteva: {ex.Message}");
                try { await SendText(context, 500, $"Interna greska: {ex.Message}"); }
                catch { }
            }
        }

        private static async Task SendText(HttpListenerContext ctx, int statusCode, string text)
        {
            var buffer = Encoding.UTF8.GetBytes(text);
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            ctx.Response.ContentLength64 = buffer.Length;
            await ctx.Response.OutputStream.WriteAsync(buffer);
            ctx.Response.Close();
        }

        private static async Task SendJson(HttpListenerContext ctx, int statusCode, object data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var buffer = Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = buffer.Length;
            await ctx.Response.OutputStream.WriteAsync(buffer);
            ctx.Response.Close();
        }
    }
}
