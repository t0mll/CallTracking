﻿using CallTraking.NEventSocket.Common.FreeSWITCH.Channel;
using CallTraking.NEventSocket.Common.Utils.Extensions;

namespace CallTraking.NEventSocket.Common.Channels
{
    // <summary>
    /// Strongly-Typed wrapper around commonly used channel variables
    /// </summary>
    public class ChannelVariables
    {
        private readonly BaseChannel channel;

        public ChannelVariables(BaseChannel channel)
        {
            this.channel = channel;
        }

        public string this[string variableName] => this.channel.GetVariable(variableName);

        /// <summary>
        /// Gets hangup reason for the last bridge attempt
        /// </summary>
        /// <remarks>
        /// last_bridge_hangup_cause is not populated in certain cases eg. USER_NOT_REGISTERED
        /// will check originate_disposition if not present
        /// </remarks>
        public HangupCause? BridgeHangupCause => (this["last_bridge_hangup_cause"] ?? this["originate_disposition"]).HeaderToEnumOrNull<HangupCause>();

        public ulong SessionId => ulong.Parse(this["session_id"]);
    }
}
