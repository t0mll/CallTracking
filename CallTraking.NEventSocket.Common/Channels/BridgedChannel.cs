using CallTraking.NEventSocket.Common.FreeSWITCH.Messages;
using CallTraking.NEventSocket.Common.Sockets;
using Microsoft.Extensions.Logging;

namespace CallTraking.NEventSocket.Common.Channels
{
    public class BridgedChannel : BaseChannel
    {
        protected internal BridgedChannel(ILogger<Channel> logger, ChannelEvent eventMessage, EventSocket eventSocket) : base(logger, eventMessage, eventSocket)
        {
        }
    }
}
