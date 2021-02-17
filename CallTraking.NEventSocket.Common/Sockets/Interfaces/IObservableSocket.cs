using System;
using System.Threading;
using System.Threading.Tasks;

namespace CallTraking.NEventSocket.Common.Sockets.Interfaces

{
    public interface IObservableSocket
    {
        bool IsConnected { get; }

        event EventHandler Disposed;

        void Dispose();
        Task SendAsync(byte[] bytes, CancellationToken cancellationToken);
        Task SendAsync(string message, CancellationToken cancellationToken);
    }
}