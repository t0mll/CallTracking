using CallTraking.NEventSocket.Common.FreeSWITCH.Messages;

namespace CallTraking.NEventSocket.Common.FreeSWITCH.Applications.PlayGetDigits
{
    /// <summary>
    /// Represents the result of the play_and_get_digits application
    /// </summary>
    public class PlayGetDigitsResult : ApplicationResult
    {
        internal PlayGetDigitsResult(ChannelEvent eventMessage, string channelVariable) : base(eventMessage)
        {
            Digits = eventMessage.GetVariable(channelVariable);

            TerminatorUsed = eventMessage.GetVariable("read_terminator_used");

            Success = !string.IsNullOrEmpty(Digits);
        }

        /// <summary>
        /// Gets the digits returned by the application
        /// </summary>
        public string Digits { get; private set; }

        /// <summary>
        /// Gets the terminating digit inputted by the user, if any
        /// </summary>
        public string TerminatorUsed { get; private set; }
    }
}
