namespace CallTraking.NEventSocket.Common.FreeSWITCH.Channel
{
    public enum AnswerState
    {
        /// <summary>
        /// The Channel is pre answered
        /// </summary>
        Early,
        /// <summary>
        /// The Channel is Answered
        /// </summary>
        Answered,
        /// <summary>
        /// The Channel has Hung Up
        /// </summary>
        Hangup,
        /// <summary>
        /// The Channel is Ringing
        /// </summary>
        Ringing
    }
}
