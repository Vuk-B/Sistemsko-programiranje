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

        public async Task StartAsync(CancellationToken ct)
        {
            _isRunning = true;
            _listener.Start();

            Logger.Info($"Server slusa na http://localhost:5050/");
            Logger.Info($"Root direktorijum: {_rootPath}");
            Logger.Info($"Maksimalno paralelnih obrada: {_maxConcurrent}");
            Logger.Info("Pritisni Ctrl+C za gasenje servera.");

            using (ct.Register(() => Stop()))
            {
                while (_isRunning && !ct.IsCancellationRequested)
                {
                    try
                    {
                        HttpListenerContext context = await _listener.GetContextAsync().WaitAsync(ct);

                        if (!_isRunning || ct.IsCancellationRequested)
                        {
                            WriteError(context, "Server se gasi.", 503);
                            break;
                        }

                        if (_activeRequests.CurrentCount <= _maxConcurrent
                            && _activeRequests.TryAddCount())
                        {
                            Logger.Info($"Pristigao zahtev (aktivnih: {_activeRequests.CurrentCount})");

                            _ = ProcessRequestAsync(context, ct).ContinueWith(antecedent =>
                            {
                                if (antecedent.IsFaulted)
                                    Logger.Error($"Greska pri obradi zahteva: {antecedent.Exception?.GetBaseException().Message}");
                                else if (antecedent.IsCanceled)
                                    Logger.Info("Obrada zahteva otkazana.");
                            }, TaskContinuationOptions.ExecuteSynchronously);
                        }
                        else
                        {
                            Logger.Warning($"Odbijen zahtev - previse aktivnih obrada ({_activeRequests.CurrentCount})");
                            WriteError(context, "Server je zauzet. Pokusajte ponovo kasnije.", 503);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
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
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            Logger.Info("Zaustavljanje servera, ceka se obrada tekugih zahteva...");

            _activeRequests.Signal();
            _activeRequests.Wait(3000);

            _listener.Stop();
            _listener.Close();

            Logger.Info("Server zaustavljen.");
        }

        private async Task ProcessRequestAsync(HttpListenerContext context, CancellationToken ct)
        {
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                ct.ThrowIfCancellationRequested();

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
                    await context.Response.OutputStream.WriteAsync(helpBuffer, 0, helpBuffer.Length, ct);
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

                var response = await CacheService.GetOrAddAsync(
                    fileName.ToLower(),
                    name => FileService.FindAndSortAsync(name, _rootPath, ct),
                    ct);

                byte[] buffer = Encoding.UTF8.GetBytes($"Sortirane reci:\n{response.Value}");
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length, ct);

                sw.Stop();

                if (response.IsFromCache)
                {
                    double speedup = (double)response.ProcessingTime / Math.Max(sw.ElapsedMilliseconds, 1);
                    Logger.Info($"[PERFORMANSE] Cache HIT ubrzanje: {speedup:F1}x (original: {response.ProcessingTime}ms, sad: {sw.ElapsedMilliseconds}ms)");
                }
                else
                {
                    Logger.Info($"[PERFORMANSE] Cache MISS (obrada trajala: {response.ProcessingTime}ms)");
                }

                Logger.Info($"Kraj obrade: {fileName} -> 200 ({sw.ElapsedMilliseconds}ms)");
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                Logger.Info($"Obrada otkazana ({sw.ElapsedMilliseconds}ms)");
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
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length, ct);

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
