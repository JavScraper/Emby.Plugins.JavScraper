using MihaZupan.Enums;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using MihaZupan.Dns;

namespace MihaZupan
{
    internal static class Helpers
    {
        public static SocketConnectionResult ToConnectionResult(this SocketException exception)
        {
            if (exception.SocketErrorCode == SocketError.ConnectionRefused)
                return SocketConnectionResult.ConnectionRefused;

            if (exception.SocketErrorCode == SocketError.HostUnreachable)
                return SocketConnectionResult.HostUnreachable;

            return SocketConnectionResult.ConnectionError;
        }

        public static bool ContainsDoubleNewLine(this byte[] buffer, int offset, int limit, out int endOfHeader)
        {
            const byte R = (byte)'\r';
            const byte N = (byte)'\n';

            bool foundOne = false;
            for (endOfHeader = offset; endOfHeader < limit; endOfHeader++)
            {
                if (buffer[endOfHeader] == N)
                {
                    if (foundOne)
                    {
                        endOfHeader++;
                        return true;
                    }
                    foundOne = true;
                }
                else if (buffer[endOfHeader] != R)
                {
                    foundOne = false;
                }
            }

            return false;
        }

        private static readonly string[] HopByHopHeaders = new string[]
        {
            // ref: https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers
            "CONNECTION", "KEEP-ALIVE", "PROXY-AUTHENTICATE", "PROXY-AUTHORIZATION", "TE", "TRAILER", "TRANSFER-ENCODING", "UPGRADE"
        };
        public static bool IsHopByHopHeader(this string header)
            => HopByHopHeaders.Contains(header, StringComparer.OrdinalIgnoreCase);

        public static AddressType GetAddressType(string hostname)
        {
            if (IPAddress.TryParse(hostname, out IPAddress hostIP))
            {
                if (hostIP.AddressFamily == AddressFamily.InterNetwork)
                {
                    return AddressType.IPv4;
                }
                else
                {
                    return AddressType.IPv6;
                }
            }
            return AddressType.DomainName;
        }
        public static void TryDispose(this Socket socket)
        {
            if (socket is null)
                return;

            if (socket.Connected)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Send);
                }
                catch { }
            }
            try
            {
                socket.Close();
            }
            catch { }
        }
        public static void TryDispose(this SocketAsyncEventArgs saea)
        {
            if (saea is null)
                return;

            try
            {
                saea.UserToken = null;
                saea.AcceptSocket = null;

                saea.Dispose();
            }
            catch { }
        }
    }
}
