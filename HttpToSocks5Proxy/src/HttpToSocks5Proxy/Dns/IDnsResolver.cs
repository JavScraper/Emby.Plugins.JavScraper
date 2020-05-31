using System.Net;

namespace MihaZupan.Dns
{
    public interface IDnsResolver
    {
       IPAddress TryResolve(string hostname);
    }
}
