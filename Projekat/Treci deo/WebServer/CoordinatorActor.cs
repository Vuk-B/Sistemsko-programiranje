using Akka.Actor;
using Akka.Event;

namespace WebServer
{
    public class CoordinatorActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private readonly YouTubeService _youTubeService;
        private readonly NerService _nerService;

        private readonly Dictionary<string, IActorRef> _videoActors = new();
        private readonly Dictionary<IActorRef, string> _actorToVideo = new();

        public CoordinatorActor(YouTubeService youTubeService, NerService nerService)
        {
            _youTubeService = youTubeService;
            _nerService = nerService;

            Receive<CommentsBatch>(msg =>
            {
                if (_videoActors.TryGetValue(msg.VideoId, out var actor))
                {
                    actor.Forward(msg);
                }
                else
                {
                    _log.Warning($"Primljen CommentsBatch za nepoznat videoId: {msg.VideoId}");
                }
            });

            Receive<GetNerResult>(msg =>
            {
                if (_videoActors.TryGetValue(msg.VideoId, out var actor))
                {
                    actor.Forward(msg);
                    return;
                }

                _log.Info($"Kreiram CommentActor za video {msg.VideoId} (prvi zahtev).");

                var props = CommentActor.Props(msg.VideoId, _youTubeService, _nerService)
                    .WithDispatcher("ner-dispatcher");

                var child = Context.ActorOf(props, $"comment-{msg.VideoId}");
                Context.Watch(child);

                _videoActors[msg.VideoId] = child;
                _actorToVideo[child] = msg.VideoId;

                Sender.Tell(new NerNotReady(msg.VideoId));
            });

            Receive<Terminated>(t =>
            {
                if (_actorToVideo.TryGetValue(t.ActorRef, out var videoId))
                {
                    _actorToVideo.Remove(t.ActorRef);
                    _videoActors.Remove(videoId);
                    _log.Info($"CommentActor za video {videoId} uklonjen iz registra.");
                }
            });
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return new OneForOneStrategy(
                maxNrOfRetries: 5,
                withinTimeRange: TimeSpan.FromMinutes(1),
                decider: Decider.From(ex => ex switch
                {
                    ArgumentException => Directive.Resume,
                    _ => Directive.Restart
                }));
        }

        public static Props Props(YouTubeService yt, NerService ner)
            => Akka.Actor.Props.Create(() => new CoordinatorActor(yt, ner));
    }
}
