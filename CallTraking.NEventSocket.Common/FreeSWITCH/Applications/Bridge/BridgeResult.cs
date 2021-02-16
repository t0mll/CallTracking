using CallTraking.NEventSocket.Common.FreeSWITCH.Headers;
using CallTraking.NEventSocket.Common.FreeSWITCH.Messages;

namespace CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Bridge
{
    /// <summary>
    /// Represents the result of a Bridge attempt.
    /// </summary>
    public class BridgeResult : ApplicationResult
    {
        internal BridgeResult(ChannelEvent eventMessage) : base(eventMessage)
        {
            if (eventMessage != null)
            {
                Success = eventMessage.Headers.ContainsKey(HeaderNames.OtherLegUniqueId);
                ResponseText = eventMessage.GetVariable("DIALSTATUS");

                if (Success)
                {
                    BridgeUUID = eventMessage.Headers[HeaderNames.OtherLegUniqueId];
                }
            }
            else
            {
                Success = false;
                ResponseText = "Aborted";
            }
        }

        /// <summary>
        /// Gets the UUID of the B-Leg Channel.
        /// </summary>
        public string BridgeUUID { get; private set; }
    }
}
