using Akka.Actor;
using Akka.Event;

namespace WebServer
{
    public class CommentActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private readonly string _videoId;
        private readonly YouTubeService _youTubeService;
        private readonly NerService _nerService;

        private List<string> _comments = new();
        private List<NamedEntity> _entities = new();
        private bool _nerReady;
        private readonly List<IActorRef> _pendingRequesters = new();

        public CommentActor(string videoId, YouTubeService youTubeService, NerService nerService)
        {
            _videoId = videoId;
            _youTubeService = youTubeService;
            _nerService = nerService;

            Receive<CommentsBatch>(batch =>
            {
                _comments = batch.Comments;
                _log.Info($"Primljeno {_comments.Count} komentara za video {_videoId}.");

                var self = Self;
                Task.Run(() => _nerService.AnalyzeAsync(_comments))
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            _log.Error(t.Exception, $"NER analiza nije uspela za {_videoId}.");
                            self.Tell(new NerAnalysisComplete(_videoId, new List<NamedEntity>()));
                        }
                        else
                        {
                            self.Tell(new NerAnalysisComplete(_videoId, t.Result));
                        }
                    });
            });

            Receive<NerAnalysisComplete>(result =>
            {
                _entities = result.Entities;
                _nerReady = true;
                _log.Info($"NER analiza zavrsena za {_videoId}: pronadjeno {_entities.Count} entiteta.");

                BroadcastToPending();
            });

            Receive<GetNerResult>(_ =>
            {
                if (_nerReady)
                {
                    Sender.Tell(new NerSuccess(_videoId, _entities));
                    _log.Info($"Vraceni NER rezultati za {_videoId} ({_entities.Count} entiteta).");
                }
                else
                {
                    _pendingRequesters.Add(Sender);
                    _log.Info($"NER jos nije gotov za {_videoId}, zahtev dodat u red cekanja.");
                }
            });
        }

        protected override void PreStart()
        {
            _youTubeService.Track(_videoId);
            _log.Info($"CommentActor za video {_videoId} pokrenut, zapoceto pracenje.");
        }

        protected override void PostStop()
        {
            _log.Info($"CommentActor za video {_videoId} zaustavljen.");
        }

        private void BroadcastToPending()
        {
            var result = new NerSuccess(_videoId, _entities);
            foreach (var requester in _pendingRequesters)
                requester.Tell(result);
            _pendingRequesters.Clear();
        }

        public static Props Props(string videoId, YouTubeService yt, NerService ner)
            => Akka.Actor.Props.Create(() => new CommentActor(videoId, yt, ner));
    }
}
