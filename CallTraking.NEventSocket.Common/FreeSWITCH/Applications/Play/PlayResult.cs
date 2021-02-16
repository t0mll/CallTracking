using CallTraking.NEventSocket.Common.FreeSWITCH.Messages;

namespace CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Play
{
    /// <summary>
    /// Represents the result of the Play dialplan application
    /// </summary>
    public class PlayResult : ApplicationResult
    {
        internal PlayResult(ChannelEvent eventMessage) : base(eventMessage)
        {
            if (eventMessage != null)
            {
                Success = ResponseText == "FILE PLAYED";  //eventMessage.Headers[HeaderNames.ApplicationResponse] == "FILE PLAYED";
            }
            else
            {
                Success = false;
            }
        }
    }
}
