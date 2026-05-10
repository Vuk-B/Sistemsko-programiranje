namespace WebServer
{
    public static class FileService
    {
        public static string NadjiSortiraj(string imeFajla, string rootFolder)
        {
            string[] sviFajlovi = Directory.GetFiles(
                rootFolder, "*", SearchOption.AllDirectories);

            string? nadjenFajl = sviFajlovi
                .FirstOrDefault(f => Path.GetFileName(f) == imeFajla);

            if (nadjenFajl == null)
                throw new FileNotFoundException($"Fajl '{imeFajla}' nije pronadjen.");

            string sadrzaj = File.ReadAllText(nadjenFajl);

            if (string.IsNullOrWhiteSpace(sadrzaj))
                throw new InvalidOperationException($"Fajl '{imeFajla}' je prazan.");

            string[] reci = sadrzaj.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            return string.Join(" ", reci.OrderBy(w => w, StringComparer.Ordinal));
        }
    }
}
