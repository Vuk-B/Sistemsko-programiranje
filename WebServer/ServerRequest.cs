using System.Net;
namespace WebServer
{
    public class ServerRequest
    {
        public string ImeFajla { get; }
        public HttpListenerResponse Odgovor { get; }
        public ServerRequest(string imeFajla, HttpListenerResponse odgovor)
        {
            ImeFajla = imeFajla;
            Odgovor = odgovor;
        }
    }
}
