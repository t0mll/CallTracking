using CallTraking.NEventSocket.Common.FreeSWITCH.Messages;
using CallTraking.NEventSocket.Common.Sockets;
using Microsoft.Extensions.Logging;

namespace CallTraking.NEventSocket.Common.Channels
{
    public class BridgedChannel : BaseChannel
    {
        protected internal BridgedChannel(ChannelEvent eventMessage, EventSocket eventSocket, ILogger<Channel> logger = null) : base(eventMessage, eventSocket, logger)
        {
        }
    }
}
