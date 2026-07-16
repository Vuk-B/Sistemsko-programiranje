using System.Diagnostics;
using System.Net;
using System.Text;

namespace WebServer
{
    public class HttpServer
    {
        private readonly HttpListener _listener;
        private readonly string _rootPath;
        private readonly int _maxConcurrent;
        private readonly CountdownEvent _activeRequests;
        private volatile bool _isRunning;

        public HttpServer(int port = 5050, int maxConcurrent = 100)
        {
            _maxConcurrent = maxConcurrent;
            _rootPath = Path.Combine(Directory.GetCurrentDirectory(), "Dokumenta");
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _activeRequests = new CountdownEvent(1);
        }

        public void Start()
        {
            _isRunning = true;
            _listener.Start();

            Logger.Info($"Server slusa na http://localhost:5050/");
            Logger.Info($"Root direktorijum: {_rootPath}");
            Logger.Info($"Maksimalno paralelnih obrada: {_maxConcurrent}");
            Logger.Info("Pritisni Ctrl+C za gasenje servera.");

            while (_isRunning)
            {
                try
                {
                    HttpListenerContext context = _listener.GetContext();

                    if (_activeRequests.CurrentCount <= _maxConcurrent
                        && _activeRequests.TryAddCount())
                    {
                        Logger.Info($"Pristigao zahtev (aktivnih: {_activeRequests.CurrentCount})");
                        ThreadPool.QueueUserWorkItem(ProcessRequest, context);
                    }
                    else
                    {
                        Logger.Warning($"Odbijen zahtev - previse aktivnih obrada ({_activeRequests.CurrentCount})");
                        WriteError(context, "Server je zauzet. Pokusajte ponovo kasnije.", 503);
                    }
                }
                catch (HttpListenerException) when (!_isRunning)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                        Logger.Error($"Greska pri prihvatanju konekcije: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            Logger.Info("Zaustavljanje servera, ceka se obrada tekugih zahteva...");

            _activeRequests.Signal();
            bool finished = _activeRequests.Wait(3000);

            _listener.Stop();
            _listener.Close();

            if (finished)
                Logger.Info("Server zaustavljen.");
            else
                Logger.Warning("Vreme za gasenje isteklo - neki zahtevi mozda nisu zavrseni.");
        }

        private void ProcessRequest(object? state)
        {
            var context = (HttpListenerContext)state!;
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                string rawPath = context.Request.Url!.AbsolutePath;

                if (rawPath == "/" || string.IsNullOrEmpty(rawPath.TrimStart('/')))
                {
                    byte[] helpBuffer = Encoding.UTF8.GetBytes(
                        "Web server za leksikografsko sortiranje reci.\n" +
                        "Upotreba: http://localhost:5050/<ime_fajla>\n" +
                        "Server pretrazuje direktorijum Dokumenta i sve poddirektorijume.");
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "text/plain";
                    context.Response.ContentLength64 = helpBuffer.Length;
                    context.Response.OutputStream.Write(helpBuffer, 0, helpBuffer.Length);
                    context.Response.Close();
                    return;
                }

                string fileName = rawPath.TrimStart('/');

                if (string.Equals(fileName, "favicon.ico", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Close();
                    return;
                }

                Logger.Info($"Pocetak obrade: {fileName}");

                var result = CacheService.GetOrAdd(
                    fileName.ToLower(),
                    name => FileService.FindAndSort(name, _rootPath));

                byte[] buffer = Encoding.UTF8.GetBytes($"Sortirane reci:\n{result}");
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);

                sw.Stop();
                Logger.Info($"Kraj obrade: {fileName} -> 200 ({sw.ElapsedMilliseconds}ms)");
            }
            catch (FileNotFoundException ex)
            {
                WriteError(context, ex.Message, 404);
                sw.Stop();
                Logger.Info($"Kraj obrade: 404 ({sw.ElapsedMilliseconds}ms)");
            }
            catch (InvalidOperationException ex)
            {
                byte[] buffer = Encoding.UTF8.GetBytes(ex.Message);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);

                sw.Stop();
                Logger.Info($"Kraj obrade: prazan fajl -> 200 ({sw.ElapsedMilliseconds}ms)");
            }
            catch (UnauthorizedAccessException ex)
            {
                WriteError(context, ex.Message, 403);
                sw.Stop();
                Logger.Warning($"Pristup odbijen: 403 ({sw.ElapsedMilliseconds}ms)");
            }
            catch (Exception ex)
            {
                Logger.Error($"Greska: {ex.Message}");
                WriteError(context, "Interna greska servera.", 500);
                sw.Stop();
            }
            finally
            {
                try { context.Response.Close(); }
                catch { }

                _activeRequests.Signal();
            }
        }

        private static void WriteError(HttpListenerContext context, string message, int statusCode)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "text/plain";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Logger.Error($"Slanje odgovora o gresci nije uspelo: {ex.Message}");
            }
        }
    }
}
