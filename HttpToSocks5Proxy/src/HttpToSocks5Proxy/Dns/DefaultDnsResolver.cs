using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace MihaZupan.Dns
{
    internal class DefaultDnsResolver : IDnsResolver
    {
        public IPAddress TryResolve(string hostname)
        {
            IPAddress result = null;

            if (IPAddress.TryParse(hostname, out var address))
            {
                return address;
            }

            try
            {
                result = System.Net.Dns.GetHostAddresses(hostname).FirstOrDefault();
            }
            catch (SocketException)
            {
                // ignore
            }

            return result;
        }
    }
}
