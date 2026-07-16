namespace WebServer
{
    public static class Config
    {
        public const string ServerPrefix = "http://localhost:5050/";

        public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(30);

        public const string YouTubeApiUrl = "https://www.googleapis.com/youtube/v3/commentThreads";

        public static string GetYouTubeApiKey()
        {
            var env = Environment.GetEnvironmentVariable("YOUTUBE_API_KEY");
            if (!string.IsNullOrEmpty(env))
                return env;

            var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    if (line.StartsWith("YOUTUBE_API_KEY="))
                        return line.Substring("YOUTUBE_API_KEY=".Length).Trim('"', '\'');
                }
            }

            return "";
        }
    }
}
