namespace WebServer
{
    public class CachedResponse
    {
        public string Value { get; }
        public DateTime CreatedAt { get; }

        public CachedResponse(string value)
        {
            Value = value;
            CreatedAt = DateTime.Now;
        }
    }
}
