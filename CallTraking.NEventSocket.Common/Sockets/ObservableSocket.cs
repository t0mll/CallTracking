using CallTraking.NEventSocket.Common.Sockets.Interfaces;
using CallTraking.NEventSocket.Common.Utils;
using CallTraking.NEventSocket.Common.Utils.Extensions;
using CallTraking.NEventSocket.Common.Utils.ObjectPooling;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.PlatformServices;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CallTraking.NEventSocket.Common.Sockets
{
    /// <summary>
    /// Wraps a <seealso cref="TcpClient"/> exposing incoming strings as an Observable sequence.
    /// </summary>
    public abstract class ObservableSocket : IDisposable, IObservableSocket
    {
        private static long _idCounter = 0;
        protected readonly long Id;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1);
        private readonly InterlockedBoolean _disposed = new InterlockedBoolean();
        private readonly InterlockedBoolean _isStarted = new InterlockedBoolean();
        private readonly CancellationTokenSource _readCancellationToken = new CancellationTokenSource();
        private TcpClient _tcpClient;
        private Subject<byte[]> _subject;
        private IObservable<byte[]> _receiver;
        static ObservableSocket()
        {
            PlatformEnlightenmentProvider.Current = new CurrentPlatformEnlightenmentProvider();
        }

        protected ObservableSocket(TcpClient tcpClient, ILogger logger = null)
        {
            _logger = logger;
            _tcpClient = tcpClient;
            _subject = new Subject<byte[]>();

            Id = Interlocked.Increment(ref _idCounter);

            _receiver = Observable.Defer(() =>
                {
                    if (_isStarted.EnsureCalledOnce())
                    {
                        return _subject.AsObservable();
                    }

                    Task.Run(
                        async () =>
                        {
                            _logger?.LogTrace($"Worker Thread {Id} started");

                            int bytesRead = 1;
                            var stream = tcpClient.GetStream();
                            byte[] buffer = SharedPools.ByteArray.Allocate();
                            try
                            {
                                while (bytesRead > 0)
                                {
                                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, _readCancellationToken.Token);
                                    if (bytesRead > 0)
                                    {
                                        if (bytesRead == buffer.Length)
                                        {
                                            _subject.OnNext(buffer);
                                        }
                                        else
                                        {
                                            _subject.OnNext(buffer.Take(bytesRead).ToArray());
                                        }
                                    }
                                    else
                                    {
                                        _subject.OnCompleted();
                                    }
                                }
                            }
                            catch (ObjectDisposedException)
                            {
                                // expected - normal shutdown
                                _subject.OnCompleted();
                            }
                            catch (TaskCanceledException)
                            {
                                // expected - normal shutdown
                                _subject.OnCompleted();
                            }
                            catch (IOException ex)
                            {
                                if (ex.InnerException is ObjectDisposedException)
                                {
                                    // expected - normal shutdown
                                    _subject.OnCompleted();
                                }
                                else
                                {
                                    // socket comms interrupted - propogate the error up the layers
                                    _logger?.LogError(ex, "IO Error reading from stream");
                                    _subject.OnError(ex);
                                }
                            }
                            catch (SocketException ex)
                            {
                                // socket comms interrupted - propogate the error up the layers
                                _logger?.LogError(ex, "Socket Error reading from stream");
                                _subject.OnError(ex);
                            }
                            catch (Exception ex)
                            {
                                // unexpected error
                                _logger?.LogError(ex, "Unexpected Error reading from stream");
                                _subject.OnError(ex);
                            }
                            finally
                            {
                                SharedPools.ByteArray.Free(buffer);

                                _logger?.LogTrace($"Worker Thread {Id} completed");
                                Dispose();
                            }
                        });

                    return _subject.AsObservable();
                });
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ObservableSocket"/> class.
        /// </summary>
        ~ObservableSocket()
        {
            Dispose(false);
        }

        /// <summary>
        /// Occurs when the <see cref="ObservableSocket"/> is disposed.
        /// </summary>
        public event EventHandler Disposed = (sender, args) => { };

        /// <summary>
        /// Gets a value indicating whether this instance is connected.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is connected; otherwise, <c>false</c>.
        /// </value>
        public bool IsConnected
        {
            get
            {
                return _tcpClient != null && _tcpClient.Connected;
            }
        }

        /// <summary>
        /// Gets an Observable sequence of byte array chunks as read from the socket stream.
        /// </summary>
        protected IObservable<byte[]> Receiver
        {
            get
            {
                return _receiver;
            }
        }

        /// <summary>
        /// Asynchronously writes the given message to the socket.
        /// </summary>
        /// <param name="message">The string message to send</param>
        /// <param name="cancellationToken">A CancellationToken to cancel the send operation.</param>
        /// <returns>A Task.</returns>
        /// <exception cref="ObjectDisposedException">If disposed.</exception>
        /// <exception cref="InvalidOperationException">If not connected.</exception>
        public Task SendAsync(string message, CancellationToken cancellationToken)
        {
            return SendAsync(Encoding.ASCII.GetBytes(message), cancellationToken);
        }

        /// <summary>
        /// Asynchronously writes the given bytes to the socket.
        /// </summary>
        /// <param name="bytes">The raw byts to stream through the socket.</param>
        /// <param name="cancellationToken">A CancellationToken to cancel the send operation.</param>
        /// <returns>A Task.</returns>
        /// <exception cref="ObjectDisposedException">If disposed.</exception>
        /// <exception cref="InvalidOperationException">If not connected.</exception>
        public async Task SendAsync(byte[] bytes, CancellationToken cancellationToken)
        {
            if (_disposed.Value)
            {
                throw new ObjectDisposedException(ToString());
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected");
            }

            try
            {
                await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                var stream = GetStream();
                await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                _logger?.LogWarning("Network Stream Disposed.");
                Dispose();
            }
            catch (TaskCanceledException)
            {
                _logger?.LogWarning("Write operation was cancelled.");
                Dispose();
            }
            catch (IOException ex)
            {
                if (ex.InnerException is SocketException
                    && ((SocketException)ex.InnerException).SocketErrorCode == SocketError.ConnectionAborted)
                {
                    _logger?.LogWarning("Socket disconnected");
                    Dispose();
                    return;
                }

                throw;
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.ConnectionAborted)
                {
                    _logger?.LogWarning("Socket disconnected");
                    Dispose();
                    return;
                }

                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error writing");
                Dispose();
                throw;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the underlying network stream.
        /// </summary>
        protected virtual Stream GetStream()
        {
            return _tcpClient.GetStream();
        }


        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed",
            MessageId = "received",
            Justification = "received is disposed of asynchronously, when the buffer has been flushed out by the consumers")]
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed != null && !_disposed.EnsureCalledOnce())
            {
                if (disposing)
                {
                    _logger?.LogTrace($"Disposing (disposing:{disposing})");
                }

                if (IsConnected)
                {
                    _readCancellationToken.Cancel();

                    if (_tcpClient != null)
                    {
                        _tcpClient.Close();
                        _tcpClient = null;

                        _logger?.LogTrace("TcpClient closed");
                    }
                }

                var localCopy = Disposed;
                if (localCopy != null)
                {
                    localCopy(this, EventArgs.Empty);
                }

                _logger?.LogDebug($"({Id}) Disposed");
            }
        }
    }
}
