namespace CallTraking.NEventSocket.Common.FreeSWITCH
{
    /// <summary>
    /// Defines the Sip Caller Id type for a Bridge or Originate
    /// See https://wiki.freeswitch.org/wiki/Variable_sip_cid_type
    /// </summary>
    public enum SIPCallerIdType
    {
        /// <summary>
        /// Uses the Remote-Party-ID header (default)
        /// </summary>
        RPid,

        /// <summary>
        /// Uses the P-Asserted-Identity header
        /// </summary>
        Pid,

        /// <summary>
        /// Places caller id in the SIP From field
        /// </summary>
        None
    }
}
