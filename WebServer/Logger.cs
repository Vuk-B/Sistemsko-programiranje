namespace WebServer
{
    public static class Logger
    {
        private static readonly object _lock = new();

        public static void Log(string poruka)
        {
            lock (_lock)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {poruka}");
            }
        }
    }
}
