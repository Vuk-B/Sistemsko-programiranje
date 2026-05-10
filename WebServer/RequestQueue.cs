namespace WebServer
{
    internal class RequestQueue
    {
        private readonly Queue<ServerRequest> _red = new();
        private readonly object _lock = new();
        private readonly int _kapacitet;
        
        internal RequestQueue(int kapacitet)
        {
            _kapacitet = kapacitet;
        }
        
        internal void Enqueue(ServerRequest zahtev)
        {
            lock (_lock)
            {
                while (_red.Count >= _kapacitet)
                    Monitor.Wait(_lock);
                _red.Enqueue(zahtev);
                Monitor.Pulse(_lock);
            }
        }
        
        internal ServerRequest Dequeue()
        {
            lock (_lock)
            {
                while (_red.Count == 0)
                    Monitor.Wait(_lock);
                ServerRequest request = _red.Dequeue();
                Monitor.Pulse(_lock);
                return request;
            }
        }
    }
}

