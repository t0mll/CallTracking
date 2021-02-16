using CallTraking.NEventSocket.Common.Channels;
using Microsoft.Extensions.Logging;
using System;
using System.Reactive.Linq;

namespace CallTraking.NEventSocket.Common.Sockets
{
    /// <summary>
    /// Listens for Outbound connections from FreeSwitch, providing notifications via the Connections observable.
    /// </summary>
    public class OutboundListener : ObservableListener<OutboundSocket>
    {
        private readonly ILogger<OutboundSocket> _logger;
        private readonly IObservable<Channel> channels;

        /// <summary>
        /// Initializes a new OutboundListener on the given port.
        /// Pass 0 as the port to auto-assign a dynamic port. Usually used for testing.
        /// </summary>
        /// <param name="port">The Tcp port to listen on.</param>
        public OutboundListener(ILogger<OutboundSocket> logger, int port) : base(logger, port, tcpClient => new OutboundSocket(tcpClient, logger))
        {
            _logger = logger;

            channels = Connections.SelectMany(
                    async socket =>
                    {
                        await socket.Connect().ConfigureAwait(false);
                        return await Channel.Create(null, socket).ConfigureAwait(false);
                    });
        }

        /// <summary>
        /// Gets an observable sequence of incoming calls wrapped as <seealso cref="Channel"/> abstractions.
        /// </summary>
        public IObservable<Channel> Channels
        {
            get
            {
                //if there is an error connecting the channel, eg. FS hangs up and goes away
                //before we can do the connect/channel_data handshake
                //then carry on allowing new connections
                return channels
                    .Do(_ => { }, ex => _logger.LogError(ex, "Unable to connect Channel"))
                    .Retry();
            }
        }
    }
}
