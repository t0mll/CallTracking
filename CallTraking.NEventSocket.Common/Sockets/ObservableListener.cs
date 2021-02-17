using CallTraking.NEventSocket.Common.Utils;
using CallTraking.NEventSocket.Common.Utils.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.PlatformServices;
using System.Reactive.Subjects;

namespace CallTraking.NEventSocket.Common.Sockets
{
    /// <summary>
    /// A Reactive wrapper around a TcpListener
    /// </summary>
    /// <typeparam name="T">The type of <seealso cref="ObservableSocket"/> that this listener will provide.</typeparam>
    public abstract class ObservableListener<T> : IDisposable where T : ObservableSocket
    {
        private readonly ILogger _logger;
        private readonly Subject<Unit> _listenerTermination = new Subject<Unit>();
        private readonly List<T> _connections = new List<T>();
        private readonly Subject<T> _observable = new Subject<T>();
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private readonly int _port;
        private readonly Func<TcpClient, T> _observableSocketFactory;
        private readonly InterlockedBoolean _disposed = new InterlockedBoolean();
        private IDisposable _subscription;
        private TcpListener _tcpListener;
        private readonly InterlockedBoolean _isStarted = new InterlockedBoolean();

        static ObservableListener()
        {
            //we need this to work around issues ilmerging rx assemblies
            PlatformEnlightenmentProvider.Current = new CurrentPlatformEnlightenmentProvider();
        }

        /// <summary>
        /// Starts the Listener on the given port
        /// </summary>
        /// <param name="port">The Tcp Port on which to listen for incoming connections.</param>
        /// <param name="observableSocketFactory">A function returning an object that inherits from <seealso cref="ObservableSocket" />.</param>
        protected ObservableListener(int port, Func<TcpClient, T> observableSocketFactory, ILogger logger = null)
        {
            _logger = logger;
            _port = port;
            _observableSocketFactory = observableSocketFactory;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ObservableListener{T}"/> class.
        /// </summary>
        ~ObservableListener()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets an observable sequence of all outbound connections from FreeSwitch.
        /// </summary>
        public IObservable<T> Connections
        {
            get
            {
                return _observable;
            }
        }

        /// <summary>
        /// Gets the Tcp Port that the Listener is waiting for connections on.
        /// </summary>
        public int Port
        {
            get
            {
                return ((IPEndPoint)_tcpListener.LocalEndpoint).Port;
            }
        }

        public bool IsStarted
        {
            get
            {
                return _isStarted.Value;
            }
        }

        /// <summary>
        /// Starts the Listener
        /// </summary>
        public void Start()
        {
            if (_disposed.Value)
            {
                throw new ObjectDisposedException(ToString());
            }

            if (_isStarted.EnsureCalledOnce())
            {
                return;
            }

            _tcpListener = new TcpListener(IPAddress.Any, _port);
            _tcpListener.Start();
            _logger?.LogTrace($"Listener Started on Port {_port}");

            _subscription = Observable.FromAsync(_tcpListener.AcceptTcpClientAsync)
                .Repeat()
                .TakeUntil(_listenerTermination)
                .Do(connection => _logger?.LogTrace($"New Connection from {connection.Client.RemoteEndPoint}"))
                .Select(
                    tcpClient =>
                    {
                        try
                        {
                            return _observableSocketFactory(tcpClient);
                        }
                        catch (Exception ex)
                        {
                            //race condition - socket might shut down before we can initialize
                            _logger?.LogError(ex, "Unable to create observableSocket");
                            return null;
                        }
                    })
                .Where(x => x != null)
                .Subscribe(
                    connection =>
                    {
                        if (connection != null)
                        {
                            _connections.Add(connection);
                            _observable.OnNext(connection);

                            _disposables.Add(
                                Observable.FromEventPattern(h => connection.Disposed += h, h => connection.Disposed -= h)
                                    .FirstAsync()
                                    .Subscribe(
                                        _ =>
                                        {
                                            _logger?.LogTrace("Connection Disposed");
                                            _connections.Remove(connection);
                                        }));
                        }
                    },
                    ex =>
                    {
                        //ObjectDisposedException is thrown by TcpListener when Stop() is called before EndAcceptTcpClient()
                        if (!(ex is ObjectDisposedException))
                        {
                            _logger?.LogError(ex, "Error handling inbound connection");
                        }
                    },
                    () => _isStarted.Set(false));

        }

        public void Stop()
        {
            if (_tcpListener != null)
            {
                _tcpListener.Stop();
                _isStarted.Set(false);
                _logger?.LogTrace("Listener stopped");
            }
        }

        /// <summary>
        /// Stops and closes down the Listener.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed != null && !_disposed.EnsureCalledOnce())
            {
                _logger?.LogTrace($"Disposing (disposing:{disposing})");

                if (disposing)
                {
                    _listenerTermination.OnNext(Unit.Default);
                    _listenerTermination.Dispose();

                    if (_subscription != null)
                    {
                        _subscription.Dispose();
                        _subscription = null;
                    }

                    _disposables.Dispose();
                    _connections?.ToList().ForEach(connection => connection?.Dispose());

                    _observable.OnCompleted();
                    _observable.Dispose();

                    if (_tcpListener != null)
                    {
                        _tcpListener.Stop();
                        _tcpListener = null;
                    }
                }

                _logger?.LogTrace("Disposed");
            }
        }
    }
}
