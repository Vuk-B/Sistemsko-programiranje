using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using Akka.Actor;

namespace WebServer
{
    public class YouTubeService : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly ReplaySubject<string> _videoIds = new();
        private IDisposable? _subscription;

        public YouTubeService()
        {
            _http = new HttpClient();
            _apiKey = Config.GetYouTubeApiKey();

            if (string.IsNullOrEmpty(_apiKey))
                Logger.Warning("YouTube API kljuc nije podesen.");
            else
                Logger.Info("YouTube API kljuc ucitan.");
        }

        public void Start(IActorRef targetActor)
        {
            _subscription = _videoIds
                .Distinct()
                .SelectMany(id => BuildStreamFor(id, targetActor))
                .SubscribeOn(TaskPoolScheduler.Default)
                .ObserveOn(TaskPoolScheduler.Default)
                .Subscribe(
                    _ => { },
                    ex => Logger.Error($"[Rx] Neocekivana greska u toku: {ex.Message}"));

            Logger.Info("[Rx] YouTube komentar stream pokrenut.");
        }

        public void Track(string videoId)
        {
            Logger.Info($"[Rx] Dodajem video {videoId} u pracenje.");
            _videoIds.OnNext(videoId);
        }

        private IObservable<CommentsBatch> BuildStreamFor(string videoId, IActorRef target)
        {
            return Observable
                .Timer(TimeSpan.Zero, Config.PollInterval, TaskPoolScheduler.Default)
                .SelectMany(_ => Observable.FromAsync(ct => FetchApiAsync(videoId, ct)))
                .Where(comments => comments.Count > 0)
                .Select(comments => new CommentsBatch(videoId, comments))
                .Do(batch =>
                {
                    target.Tell(batch);
                    Logger.Info($"[Rx] Prosledjeno {batch.Comments.Count} komentara za video {videoId}.");
                });
        }

        private async Task<List<string>> FetchApiAsync(string videoId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Logger.Warning($"[YouTube API] API kljuc nije podesen, preskacem video {videoId}.");
                return new List<string>();
            }

            try
            {
                string url = $"{Config.YouTubeApiUrl}?part=snippet&videoId={videoId}&maxResults=100&key={_apiKey}";

                var response = await _http.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);

                var comments = new List<string>();

                if (doc.RootElement.TryGetProperty("items", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        var text = item
                            .GetProperty("snippet")
                            .GetProperty("topLevelComment")
                            .GetProperty("snippet")
                            .GetProperty("textDisplay")
                            .GetString();

                        if (!string.IsNullOrWhiteSpace(text))
                            comments.Add(text);
                    }
                }

                return comments;
            }
            catch (OperationCanceledException)
            {
                return new List<string>();
            }
            catch (Exception ex)
            {
                Logger.Error($"[YouTube API] Greska za video {videoId}: {ex.Message}");
                return new List<string>();
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _videoIds.Dispose();
            _http.Dispose();
        }
    }
}
