namespace WebServer
{
    public record NamedEntity(string Text, string Type);
    public record CommentsBatch(string VideoId, List<string> Comments);
    public record NerAnalysisComplete(string VideoId, List<NamedEntity> Entities);
    public record GetNerResult(string VideoId);

    public interface INerResponse
    {
        string VideoId { get; }
    }

    public record NerSuccess(string VideoId, List<NamedEntity> Entities) : INerResponse;
    public record NerNotReady(string VideoId) : INerResponse;
    public record NerError(string VideoId, string Message) : INerResponse;
}
