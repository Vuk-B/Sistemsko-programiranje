using System;

namespace WebServer
{
    public static class FileService
    {
        public static async Task<string> FindAndSortAsync(string fileName, string rootFolder, CancellationToken ct)
        {
            string rootDir = Path.GetFullPath(rootFolder);
            if (!rootDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
                rootDir += Path.DirectorySeparatorChar;

            string fileNameOnly = Path.GetFileName(fileName);
            string targetPath = Path.GetFullPath(Path.Combine(rootDir, fileNameOnly));

            if (!targetPath.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException(
                    $"Pristup fajlu '{fileName}' nije dozvoljen.");

            string[] allFiles = Directory.GetFiles(
                rootFolder, "*", SearchOption.AllDirectories);

            string? foundFile = allFiles
                .FirstOrDefault(f => string.Equals(
                    Path.GetFileName(f), fileNameOnly, StringComparison.OrdinalIgnoreCase));

            if (foundFile == null)
                throw new FileNotFoundException(
                    $"Fajl '{fileName}' nije pronadjen.");

            string content = await File.ReadAllTextAsync(foundFile, ct);

            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException(
                    $"Fajl '{fileName}' je prazan.");

            Task<string> sortTask = Task.Run(() =>
            {
                string[] words = content.Split(
                    new char[] { ' ', '\t', '\n', '\r' },
                    StringSplitOptions.RemoveEmptyEntries);

                return string.Join(" ",
                    words.OrderBy(w => w, StringComparer.Ordinal));
            }, ct);

            return await sortTask.ContinueWith(antecedent =>
            {
                string sorted = antecedent.Result;
                int wordCount = sorted.Split(' ').Length;
                Logger.Info($"[ContinueWith] Sortirano {wordCount} reci iz fajla '{fileName}'");
                return sorted;
            }, TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
