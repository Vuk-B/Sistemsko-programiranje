using System.Net;
namespace WebServer
{
    class Program
    {
        const int KapacitetReda = 10;
        const string AdresaServera = "http://localhost:5050/";
        static readonly string rootFolder = Path.Combine(Directory.GetCurrentDirectory(), "Fajlovi");
        static readonly RequestQueue redZahteva = new(KapacitetReda);
        static void Main()
        {
            int brojRadnika = Environment.ProcessorCount;
            
            for (int i = 0; i < brojRadnika; i++)
            {
                new Thread(ProcessRequests)
                {
                    IsBackground = true,
                    Name = $"Worker-{i + 1}"
                }.Start();
            }
            var listener = new HttpListener();
            
            listener.Prefixes.Add(AdresaServera);
            listener.Start();
            
            Logger.Log("Server slusa na http://localhost:5050/");
            Logger.Log($"Root folder: {rootFolder}");
            
            while (true)
            {
                HttpListenerContext kontekst = listener.GetContext();
                string imeFajla = kontekst.Request.Url!.AbsolutePath.TrimStart('/');
                Logger.Log($"Primljen: {imeFajla}");
                var zahtev = new ServerRequest(imeFajla, kontekst.Response);
                redZahteva.Enqueue(zahtev);
            }
        
        }
       
        static void ProcessRequests()
        {
            
            while (true)
            {
                ServerRequest zahtev = redZahteva.Dequeue();
                Logger.Log($"Obrada: {zahtev.ImeFajla} ({Thread.CurrentThread.Name})");
            
                string tekstOdgovora;
                int statusniKod;
                try
                {
                    string sortirano = CacheService.GetOrAdd(
                        zahtev.ImeFajla,
                        () => FileService.NadjiSortiraj(zahtev.ImeFajla, rootFolder));
                    tekstOdgovora = $"Sortirane reci:\n{sortirano}";
                    statusniKod = 200;
                }
                catch (FileNotFoundException ex)
                {
                    tekstOdgovora = ex.Message;
                    statusniKod = 404;
                }
                catch (InvalidOperationException ex)
                {
                    tekstOdgovora = ex.Message;
                    statusniKod = 200;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Greska: {ex.Message}");
                    tekstOdgovora = "Interna greska servera.";
                    statusniKod = 500;
                }
                
                byte[] bafer = System.Text.Encoding.UTF8.GetBytes(tekstOdgovora);
                
                zahtev.Odgovor.StatusCode = statusniKod;
                zahtev.Odgovor.ContentType = "text/plain; charset=utf-8";
                zahtev.Odgovor.ContentLength64 = bafer.Length;
                
                zahtev.Odgovor.OutputStream.Write(bafer, 0, bafer.Length);
                zahtev.Odgovor.Close();

                Logger.Log($"Zavrseno: {zahtev.ImeFajla} -> {statusniKod}");

            }
        }
    }
}
