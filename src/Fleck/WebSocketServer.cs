using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;

namespace Fleck
{
    public class WebSocketServer : IWebSocketServer
    {
        private readonly string _scheme;
        private Action<IWebSocketConnection> _config;

        public WebSocketServer(string location)
            : this(8181, location)
        {
        }

        public WebSocketServer(int port, string location)
        {
            var uri = new Uri(location);
            Port = uri.Port > 0 ? uri.Port : port;
            Location = location;
            _scheme = uri.Scheme;
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            ListenerSocket = new SocketWrapper(socket);
            SupportedSubProtocols = new string[0];
        }

        public ISocket ListenerSocket { get; set; }
        public string Location { get; private set; }
        public int Port { get; private set; }
        public X509Certificate2 Certificate { get; set; }
        public IEnumerable<string> SupportedSubProtocols { get; set; }

        public bool IsSecure
        {
            get { return _scheme == "wss" && Certificate != null; }
        }

        public void Dispose()
        {
            ListenerSocket.Dispose();
        }

        public void Start(Action<IWebSocketConnection> config)
        {
            var ipLocal = new IPEndPoint(IPAddress.Any, Port);
            ListenerSocket.Bind(ipLocal);
            ListenerSocket.Listen(100);
            FleckLog.Info("Server started at " + Location);
            if (_scheme == "wss")
            {
                if (Certificate == null)
                {
                    FleckLog.Error("Scheme cannot be 'wss' without a Certificate");
                    return;
                }
            }
            ListenForClients();
            _config = config;
        }

        private void ListenForClients()
        {
            ListenerSocket.Accept(OnClientConnect, e => FleckLog.Error("Listener socket is closed", e));
        }

        private void OnClientConnect(ISocket clientSocket)
        {
            FleckLog.Debug(String.Format("Client connected from {0}:{1}", clientSocket.RemoteIpAddress, clientSocket.RemotePort.ToString()));
            ListenForClients();

            WebSocketConnection connection = null;

            connection = new WebSocketConnection(
                clientSocket,
                _config,
                bytes => RequestParser.Parse(bytes, _scheme),
                r => HandlerFactory.BuildHandler(r,
                                                 s => connection.OnMessage(s),
                                                 connection.Close,
                                                 b => connection.OnBinary(b),
                                                 p=>connection.OnPing(p)),
                s => SubProtocolNegotiator.Negotiate(SupportedSubProtocols, s));

            if (IsSecure)
            {
                FleckLog.Debug("Authenticating Secure Connection");
                clientSocket
                    .Authenticate(Certificate,
                                  connection.StartReceiving,
                                  e => FleckLog.Warn("Failed to Authenticate", e));
            }
            else
            {
                connection.StartReceiving();
            }
        }
    }
}
