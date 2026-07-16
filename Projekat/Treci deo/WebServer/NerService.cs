namespace WebServer
{
    public class NerService
    {
        public Task<List<NamedEntity>> AnalyzeAsync(List<string> comments)
        {
            return Task.FromResult(new List<NamedEntity>());
        }
    }
}
