using System;
using System.Net;
#if !PORTABLE
using System.Security.Cryptography.X509Certificates;
#else
using Windows.Networking;
#endif
using System.Threading.Tasks;
using System.IO;

namespace Fleck
{
    public interface ISocket
    {
        bool Connected { get; }
        string RemoteIpAddress { get; }
        int RemotePort { get; }
        Stream Stream { get; }
        bool NoDelay { get; set; }

        Task<ISocket> Accept(Action<ISocket> callback, Action<Exception> error);
        Task Send(byte[] buffer, Action callback, Action<Exception> error);
        Task<int> Receive(byte[] buffer, Action<int> callback, Action<Exception> error, int offset = 0);
#if !PORTABLE
        Task Authenticate(X509Certificate2 certificate, Action callback, Action<Exception> error);
#endif

        void Dispose();
        void Close();
#if PORTABLE
        void Bind(HostName ipLocal,string port);
#else
        void Bind(EndPoint ipLocal);
#endif
        void Listen(int backlog);
    }
}
