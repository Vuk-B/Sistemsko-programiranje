namespace WebServer
{
    public class CachedResponse
    {
        public string Value { get; }
        public DateTime CreatedAt { get; }
        public long ProcessingTime { get; }
        public bool IsFromCache { get; }

        public CachedResponse(string value, long processingTime = 0, bool isFromCache = false)
        {
            Value = value;
            CreatedAt = DateTime.Now;
            ProcessingTime = processingTime;
            IsFromCache = isFromCache;
        }
    }
}
