using CallTraking.NEventSocket.Common.FreeSWITCH;
using CallTraking.NEventSocket.Common.FreeSWITCH.Messages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallTraking.NEventSocket.Common.Sockets
{
    /// <summary>
    /// Represents a connection made outbound from FreeSwitch to the controlling application.
    /// </summary>
    /// <remarks>
    /// See https://wiki.freeswitch.org/wiki/Event_Socket_Outbound
    /// </remarks>
    public class OutboundSocket : EventSocket
    {
        private readonly ILogger<EventSocket> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundSocket"/> class.
        /// </summary>
        /// <param name="tcpClient">The TCP client to wrap.</param>
        protected internal OutboundSocket(TcpClient tcpClient, ILogger<EventSocket> logger = null) : base(tcpClient, logger:logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// When FS connects to an "Event Socket Outbound" handler, it sends
        /// a "CHANNEL_DATA" event in the headers of the Command-Reply received in response to Connect();
        /// </summary>
        public ChannelEvent ChannelData { get; private set; }

        /// <summary>
        /// Sends the connect command to FreeSwitch, populating the <see cref="ChannelData"/> property on reply.
        /// </summary>
        public async Task<ChannelEvent> Connect()
        {
            var response = await SendCommand("connect").ConfigureAwait(false);
            ChannelData = new ChannelEvent(response);

            var socketMode = ChannelData.GetHeader("Socket-Mode");
            var controlMode = ChannelData.GetHeader("Control");

            if (socketMode == "static")
            {
                _logger?.LogWarning("This socket is not using 'async' mode - certain dialplan applications may bock control flow");
            }

            if (controlMode != "full")
            {
                _logger?.LogDebug("This socket is not using 'full' control mode - FreeSwitch will not let you execute certain commands.");
            }

            Messages.FirstAsync(m => m.ContentType == ContentTypes.DisconnectNotice)
                            .Subscribe(dn => _logger?.LogTrace($"Channel {ChannelData.UUID} Disconnect Notice {dn.BodyText} received."));

            return ChannelData;
        }
    }
}
