using System;

namespace WebServer
{
    public static class Logger
    {
        private static readonly object _lock = new();

        public static void Info(string message) => Write("INFO", ConsoleColor.Gray, message);
        public static void Warning(string message) => Write("WARN", ConsoleColor.Yellow, message);
        public static void Error(string message) => Write("ERR", ConsoleColor.Red, message);

        private static void Write(string level, ConsoleColor color, string message)
        {
            lock (_lock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] " +
                    $"[Thread {Thread.CurrentThread.ManagedThreadId}] " +
                    $"[{level}] {message}");
                Console.ResetColor();
            }
        }
    }
}
