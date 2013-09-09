using System;
using System.IO;
using System.Net;

using System.Threading.Tasks;
using System.Threading;
using Windows.Networking.Sockets;
using Windows.Networking;
using Windows.Storage.Streams;

namespace Fleck
{
    public class SocketWrapper : ISocket
    {
        private readonly StreamSocketListener _listeningSocket;
        private StreamSocket _socket;
        private IOutputStream _stream;
        private CancellationTokenSource _tokenSource;
        private TaskFactory _taskFactory;
        private Action<ISocket> _acceptAction;
        public string RemoteIpAddress
        {
            get
            {
                var endpoint = _socket.Information.RemoteHostName;
                return endpoint != null ? endpoint.DisplayName.ToString() : null;
            }
        }

        public int RemotePort
        {
            get
            {
                var endpoint = _socket.Information.RemotePort;
                return endpoint != null ? Int32.Parse(endpoint) : -1;
            }
        }

        public SocketWrapper(SocketWrapper listeningSocket)
        {
            _tokenSource = new CancellationTokenSource();
            _taskFactory = new TaskFactory(_tokenSource.Token);
            _socket = listeningSocket._socket;
            _stream = _socket.OutputStream;
            _listeningSocket = listeningSocket._listeningSocket;
        }
        public SocketWrapper(StreamSocketListener socket)
        {
            _tokenSource = new CancellationTokenSource();
            _taskFactory = new TaskFactory(_tokenSource.Token);
            _listeningSocket = socket;
            
            _listeningSocket.ConnectionReceived += _listeningSocket_ConnectionReceived;
            
        }

        void _listeningSocket_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            _socket = args.Socket;
            _stream = _socket.OutputStream;
            
            if (_acceptAction != null)
                _acceptAction(this);
            
        }

      

        public void Listen(int backlog)
        {
            
        }

        public async void Bind(HostName endPoint,string port)
        {
            await _listeningSocket.BindServiceNameAsync(port);// BindEndpointAsync(endPoint, port);
        }

        public bool Connected
        {
            get { return true;}
        }
        
        public Stream Stream
        {
            get { return null;}
        }

        public bool NoDelay
        {
            get { return _socket.Control.NoDelay; }
            set { _socket.Control.NoDelay = value; }
        }

        public async Task<int> Receive(byte[] buffer, Action<int> callback, Action<Exception> error, int offset)
        {
            try
            {
                var ibuffer = new Windows.Storage.Streams.Buffer((uint)buffer.Length);
                DataReader reader = new DataReader(_socket.InputStream);
                reader.InputStreamOptions = InputStreamOptions.Partial;
                uint count = await reader.LoadAsync((uint)buffer.Length);
                byte[] bytes = new byte[count];
                reader.ReadBytes(bytes);
                reader.DetachStream();
                bytes.CopyTo(buffer, 0);
                //var result = await _socket.InputStream.AsStreamForRead(buffer.Length).ReadAsync(buffer, offset, buffer.Length,_tokenSource.Token);
                //var result = await _socket.InputStream.ReadAsync(ibuffer, (uint)buffer.Length, Windows.Storage.Streams.InputStreamOptions.None);
                
                callback((int)count);
                return (int)count;
                
            }
            catch (Exception e)
            {
                error(e);
                return 0;
            }
        }

        public async Task<ISocket> Accept(Action<ISocket> callback, Action<Exception> error)
        {
            _acceptAction = callback;
            return this;
        }

        public void Dispose()
        {
            _tokenSource.Cancel();
           // if (_stream != null) _stream.Dispose();
            if (_socket != null) _socket.Dispose();
            
        }

        public void Close()
        {
            _tokenSource.Cancel();
        }

        //public int EndSend(IAsyncResult asyncResult)
        //{
        //    _stream.EndWrite(asyncResult);
        //    return 0;
        //}

        public async Task Send(byte[] buffer, Action callback, Action<Exception> error)
        {
            //if (_tokenSource.IsCancellationRequested)
             //   return;
            
            
            try
            {
                DataWriter writer = new DataWriter(_stream);
                writer.WriteBytes(buffer);
                await writer.StoreAsync();
                writer.DetachStream();
                //await _socket.OutputStream.AsStreamForWrite().WriteAsync(buffer, 0, buffer.Length, _tokenSource.Token);
                
                callback();
                return;
            }
            catch (Exception e)
            {
                error(e);
                return ;
            }
        }
    }
}
