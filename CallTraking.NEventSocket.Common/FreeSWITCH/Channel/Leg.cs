namespace CallTraking.NEventSocket.Common.FreeSWITCH.Channel
{
    /// <summary>
    /// Refers to a leg of a call consisting of one or more channels
    /// </summary>
    public enum Leg
    {
        /// <summary>
        /// Both Legs of the Call
        /// </summary>
        Both,

        /// <summary>
        /// The A-Leg of the call
        /// </summary>
        ALeg,

        /// <summary>
        /// The B-Leg of the call
        /// </summary>
        BLeg,
    }
}
