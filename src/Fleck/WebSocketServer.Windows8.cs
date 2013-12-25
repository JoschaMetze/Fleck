using System;
using System.Collections.Generic;
using System.Net;
using Windows.Networking;
using Windows.Networking.Sockets;


namespace Fleck
{
    public class WebSocketServer : IWebSocketServer
    {
        private readonly string _scheme;
        private Action<IWebSocketConnection> _config;
        private StreamSocketListener _socket;
        public WebSocketServer(string location)
            : this(8181, location)
        {
        }

        public WebSocketServer(int port, string location)
        {
            var uri = new Uri(location);
            Port = uri.Port > 0 ? uri.Port : port;
            Location = uri.Host;
            _scheme = uri.Scheme;
           _socket = new StreamSocketListener();
            ListenerSocket = new SocketWrapper(_socket);
            SupportedSubProtocols = new string[0];
        }

        public ISocket ListenerSocket { get; set; }
        public string Location { get; private set; }
        public int Port { get; private set; }
        //public X509Certificate2 Certificate { get; set; }
        public IEnumerable<string> SupportedSubProtocols { get; set; }

        public bool IsSecure
        {
            get { return false; }
        }

        public void Dispose()
        {
            ListenerSocket.Dispose();
        }

        public void Start(Action<IWebSocketConnection> config)
        {
            var ipLocal = new HostName(Location);
            ListenerSocket.Bind(ipLocal,Port.ToString());
            ListenerSocket.Listen(100);
            FleckLog.Info("Server started at " + Location);
            if (_scheme == "wss")
            {

                FleckLog.Error("Scheme cannot be 'wss' without a Certificate");
                return;

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
                new SocketWrapper(clientSocket as SocketWrapper),
                _config,
                bytes => RequestParser.Parse(bytes, _scheme),
                r => HandlerFactory.BuildHandler(r,
                                                 s => connection.OnMessage(s),
                                                 connection.Close,
                                                 b => connection.OnBinary(b),
                                                 p=> connection.OnPing(p)),
                s => SubProtocolNegotiator.Negotiate(SupportedSubProtocols, s));

            if (IsSecure)
            {
                FleckLog.Debug("Authenticating Secure Connection");

                FleckLog.Warn("Failed to Authenticate");
            }
            else
            {
                connection.StartReceiving();
            }
        }
    }
}
