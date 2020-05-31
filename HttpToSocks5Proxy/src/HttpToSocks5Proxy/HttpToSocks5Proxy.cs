using System;
using System.Text;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using MihaZupan.Dns;
using MihaZupan.Enums;

namespace MihaZupan
{
    /// <summary>
    /// Presents itself as an HTTP(s) proxy, but connects to a SOCKS5 proxy behind-the-scenes
    /// </summary>
    public class HttpToSocks5Proxy : IWebProxy
    {
        /// <summary>
        /// Ignored by this <see cref="IWebProxy"/> implementation
        /// </summary>
        public ICredentials Credentials { get; set; }
        /// <summary>
        /// Returned <see cref="Uri"/> is constant for a single <see cref="HttpToSocks5Proxy"/> instance
        /// <para>Address is a local address, the port is <see cref="InternalServerPort"/></para>
        /// </summary>
        /// <param name="destination">Ignored by this <see cref="IWebProxy"/> implementation</param>
        /// <returns></returns>
        public Uri GetProxy(Uri destination) => ProxyUri;
        /// <summary>
        /// Always returns false
        /// </summary>
        /// <param name="host">Ignored by this <see cref="IWebProxy"/> implementation</param>
        /// <returns></returns>
        public bool IsBypassed(Uri host) => false;
        /// <summary>
        /// The port on which the internal server is listening
        /// </summary>
        public int InternalServerPort { get; private set; }

        /// <summary>
        /// A custom domain name resolver
        /// </summary>
        public IDnsResolver DnsResolver
        {
            set
            {
                dnsResolver = value ?? throw new ArgumentNullException(nameof(value));
            }
        }
        private IDnsResolver dnsResolver;

        private readonly Uri ProxyUri;
        private readonly Socket InternalServerSocket;

        private readonly ProxyInfo[] ProxyList;

        /// <summary>
        /// Controls whether domain names are resolved locally or passed to the proxy server for evaluation
        /// <para>False by default</para>
        /// </summary>
        public bool ResolveHostnamesLocally = false;

        #region Constructors
        /// <summary>
        /// Create an Http(s) to Socks5 proxy using no authentication
        /// </summary>
        /// <param name="socks5Hostname">IP address or hostname of the Socks5 proxy server</param>
        /// <param name="socks5Port">Port of the Socks5 proxy server</param>
        /// <param name="internalServerPort">The port to listen on with the internal server, 0 means it is selected automatically</param>
        public HttpToSocks5Proxy(string socks5Hostname, int socks5Port, int internalServerPort = 0)
            : this(new[] { new ProxyInfo(socks5Hostname, socks5Port) }, internalServerPort) { }

        /// <summary>
        /// Create an Http(s) to Socks5 proxy using username and password authentication
        /// <para>Note that many public Socks5 servers don't actually require a username and password</para>
        /// </summary>
        /// <param name="socks5Hostname">IP address or hostname of the Socks5 proxy server</param>
        /// <param name="socks5Port">Port of the Socks5 proxy server</param>
        /// <param name="username">Username for the Socks5 server authentication</param>
        /// <param name="password">Password for the Socks5 server authentication</param>
        /// <param name="internalServerPort">The port to listen on with the internal server, 0 means it is selected automatically</param>
        public HttpToSocks5Proxy(string socks5Hostname, int socks5Port, string username, string password, int internalServerPort = 0)
            : this(new[] { new ProxyInfo(socks5Hostname, socks5Port, username, password) }, internalServerPort) { }

        /// <summary>
        /// Create an Http(s) to Socks5 proxy using one or multiple chained proxies
        /// </summary>
        /// <param name="proxyList">List of proxies to route through</param>
        /// <param name="internalServerPort">The port to listen on with the internal server, 0 means it is selected automatically</param>
        public HttpToSocks5Proxy(ProxyInfo[] proxyList, int internalServerPort = 0)
        {
            if (internalServerPort < 0 || internalServerPort > 65535) throw new ArgumentOutOfRangeException(nameof(internalServerPort));
            if (proxyList == null) throw new ArgumentNullException(nameof(proxyList));
            if (proxyList.Length == 0) throw new ArgumentException("proxyList is empty", nameof(proxyList));
            if (proxyList.Any(p => p == null)) throw new ArgumentNullException(nameof(proxyList), "Proxy in proxyList is null");

            ProxyList = proxyList;
            InternalServerPort = internalServerPort;
            dnsResolver = new DefaultDnsResolver();

            InternalServerSocket = CreateSocket();
            InternalServerSocket.Bind(new IPEndPoint(IPAddress.Any, InternalServerPort));

            if (InternalServerPort == 0)
                InternalServerPort = ((IPEndPoint)(InternalServerSocket.LocalEndPoint)).Port;

            ProxyUri = new Uri("http://127.0.0.1:" + InternalServerPort);
            InternalServerSocket.Listen(8);
            InternalServerSocket.BeginAccept(OnAcceptCallback, null);
        }
        #endregion

        private void OnAcceptCallback(IAsyncResult AR)
        {
            if (Stopped) return;

            Socket clientSocket = null;
            try
            {
                clientSocket = InternalServerSocket.EndAccept(AR);
            }
            catch { }

            try
            {
                InternalServerSocket.BeginAccept(OnAcceptCallback, null);
            }
            catch { StopInternalServer(); }

            if (clientSocket != null)
                HandleRequest(clientSocket);
        }
        private void HandleRequest(Socket clientSocket)
        {
            Socket socks5Socket = null;
            bool success = true;

            try
            {
                if (TryReadTarget(clientSocket, out string hostname, out int port, out string httpVersion, out bool connect, out string request, out byte[] overRead))
                {
                    try
                    {
                        socks5Socket = CreateSocket();
                        socks5Socket.Connect(dnsResolver.TryResolve(ProxyList[0].Hostname), ProxyList[0].Port);
                    }
                    catch (SocketException ex)
                    {
                        SendError(clientSocket, ex.ToConnectionResult());
                        success = false;
                    }
                    catch (Exception)
                    {
                        SendError(clientSocket, Enums.SocketConnectionResult.UnknownError);
                        success = false;
                    }

                    if (success)
                    {
                        SocketConnectionResult result;
                        for (int i = 0; i < ProxyList.Length - 1; i++)
                        {
                            var proxy = ProxyList[i];
                            var nextProxy = ProxyList[i + 1];
                            result = Socks5.TryCreateTunnel(socks5Socket, nextProxy.Hostname, nextProxy.Port, proxy, ResolveHostnamesLocally ? dnsResolver : null);
                            if (result != SocketConnectionResult.OK)
                            {
                                SendError(clientSocket, result, httpVersion);
                                success = false;
                                break;
                            }
                        }

                        if (success)
                        {
                            var lastProxy = ProxyList.Last();
                            result = Socks5.TryCreateTunnel(socks5Socket, hostname, port, lastProxy, ResolveHostnamesLocally ? dnsResolver : null);
                            if (result != SocketConnectionResult.OK)
                            {
                                SendError(clientSocket, result, httpVersion);
                                success = false;
                            }
                            else
                            {
                                if (!connect)
                                {
                                    SendString(socks5Socket, request);
                                    if (overRead != null)
                                    {
                                        socks5Socket.Send(overRead, SocketFlags.None);
                                    }
                                }
                                else
                                {
                                    SendString(clientSocket, httpVersion + "200 Connection established\r\nProxy-Agent: MihaZupan-HttpToSocks5Proxy\r\n\r\n");
                                }
                            }
                        }
                    }
                }
                else success = false;
            }
            catch
            {
                success = false;
                try
                {
                    SendError(clientSocket, SocketConnectionResult.UnknownError);
                }
                catch { }
            }
            finally
            {
                if (success)
                {
                    SocketRelay.RelayBiDirectionally(socks5Socket, clientSocket);
                }
                else
                {
                    clientSocket.TryDispose();
                    socks5Socket.TryDispose();
                }
            }
        }

        private static bool TryReadTarget(Socket clientSocket, out string hostname, out int port, out string httpVersion, out bool connect, out string request, out byte[] overReadBuffer)
        {
            hostname = null;
            port = -1;
            httpVersion = null;
            connect = false;
            request = null;

            if (!TryReadHeaders(clientSocket, out string headerString, out overReadBuffer))
                return false;

            List<string> headerLines = headerString.Split('\n').Select(i => i.TrimEnd('\r')).Where(i => i.Length > 0).ToList();
            string[] methodLine = headerLines[0].Split(' ');
            if (methodLine.Length != 3) // METHOD URI HTTP/X.Y
            {
                SendError(clientSocket, SocketConnectionResult.InvalidRequest);
                return false;
            }
            string method = methodLine[0];
            httpVersion = methodLine[2].Trim() + " ";
            connect = method.Equals("Connect", StringComparison.OrdinalIgnoreCase);
            string hostHeader = null;

            #region Host header
            if (connect)
            {
                foreach (var headerLine in headerLines)
                {
                    int colon = headerLine.IndexOf(':');
                    if (colon == -1)
                    {
                        SendError(clientSocket, SocketConnectionResult.InvalidRequest, httpVersion);
                        return false;
                    }
                    string headerName = headerLine.Substring(0, colon).Trim();
                    if (headerName.Equals("Host", StringComparison.OrdinalIgnoreCase))
                    {
                        hostHeader = headerLine.Substring(colon + 1).Trim();
                        break;
                    }
                }
            }
            else
            {
                var hostUri = new Uri(methodLine[1]);

                StringBuilder requestBuilder = new StringBuilder();

                requestBuilder.Append(methodLine[0]);
                requestBuilder.Append(' ');
                requestBuilder.Append(hostUri.PathAndQuery);
                requestBuilder.Append(hostUri.Fragment);
                requestBuilder.Append(' ');
                requestBuilder.Append(methodLine[2]);

                for (int i = 1; i < headerLines.Count; i++)
                {
                    int colon = headerLines[i].IndexOf(':');
                    if (colon == -1) continue; // Invalid header found (no colon separator) - skip it instead of aborting the connection
                    string headerName = headerLines[i].Substring(0, colon).Trim();

                    if (headerName.Equals("Host", StringComparison.OrdinalIgnoreCase))
                    {
                        hostHeader = headerLines[i].Substring(colon + 1).Trim();
                        requestBuilder.Append("\r\n");
                        requestBuilder.Append(headerLines[i]);
                    }
                    else if (!headerName.IsHopByHopHeader())
                    {
                        requestBuilder.Append("\r\n");
                        requestBuilder.Append(headerLines[i]);
                    }
                }
                if (hostHeader == null)
                {
                    // Desperate attempt at salvaging a connection without a host header
                    requestBuilder.Append("\r\nHost: ");
                    requestBuilder.Append(hostUri.Host);
                }
                requestBuilder.Append("\r\n\r\n");
                request = requestBuilder.ToString();
            }
            #endregion Host header

            #region Hostname and port
            port = connect ? 443 : 80;

            if (string.IsNullOrEmpty(hostHeader))
            {
                // Host was not found in the host header
                string requestTarget = methodLine[1];
                hostname = requestTarget;
                int colon = requestTarget.LastIndexOf(':');
                if (colon != -1)
                {
                    if (int.TryParse(requestTarget.Substring(colon + 1), out port))
                    {
                        // A port was specified in the first line (method line)
                        hostname = requestTarget.Substring(0, colon);
                    }
                    else port = connect ? 443 : 80;
                }
            }
            else
            {
                int colon = hostHeader.LastIndexOf(':');
                if (colon == -1)
                {
                    // Host was found in the header, but we'll still look for a port in the method line
                    hostname = hostHeader;
                    string requestTarget = methodLine[1];
                    colon = requestTarget.LastIndexOf(':');
                    if (colon != -1)
                    {
                        if (!int.TryParse(requestTarget.Substring(colon + 1), out port))
                            port = connect ? 443 : 80;
                    }
                }
                else
                {
                    // Host was found in the header, it could also contain a port
                    hostname = hostHeader.Substring(0, colon);
                    if (!int.TryParse(hostHeader.Substring(colon + 1), out port))
                        port = connect ? 443 : 80;
                }
            }
            #endregion Hostname and port

            return true;
        }
        private static bool TryReadHeaders(Socket clientSocket, out string headers, out byte[] overRead)
        {
            headers = null;
            overRead = null;

            var headersBuffer = new byte[8192];
            int received = 0;
            int left = 8192;
            int offset;
            int endOfHeader;
            // According to https://stackoverflow.com/a/686243/6845657 even Apache gives up after 8KB

            do
            {
                if (left == 0)
                {
                    SendError(clientSocket, SocketConnectionResult.InvalidRequest);
                    return false;
                }
                offset = received;
                int read = clientSocket.Receive(headersBuffer, received, left, SocketFlags.None);
                if (read == 0)
                {
                    return false;
                }
                received += read;
                left -= read;
            }
            // received - 3 is used because we could have read the start of the double new line in the previous read
            while (!headersBuffer.ContainsDoubleNewLine(Math.Max(0, offset - 3), received, out endOfHeader));

            headers = Encoding.ASCII.GetString(headersBuffer, 0, endOfHeader);

            if (received != endOfHeader)
            {
                int overReadCount = received - endOfHeader;
                overRead = new byte[overReadCount];
                Array.Copy(headersBuffer, endOfHeader, overRead, 0, overReadCount);
            }

            return true;
        }

        private static void SendString(Socket socket, string text)
            => socket.Send(Encoding.UTF8.GetBytes(text));
        private static void SendError(Socket socket, SocketConnectionResult error, string httpVersion = "HTTP/1.1 ")
            => SendString(socket, ErrorResponseBuilder.Build(error, httpVersion));

        private static Socket CreateSocket()
            => new Socket(SocketType.Stream, ProtocolType.Tcp);

        private bool Stopped = false;
        public void StopInternalServer()
        {
            if (Stopped) return;
            Stopped = true;
            InternalServerSocket.Close();
        }
    }
}