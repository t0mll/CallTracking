using CallTraking.NEventSocket.Common.FreeSWITCH.Headers;
using System;

namespace CallTraking.NEventSocket.Common.FreeSWITCH.Messages
{
    /// <summary>
    /// Represents the result of a bgapi call.
    /// </summary>
    [Serializable]
    public class BackgroundJobResult : BasicMessage
    {
        internal BackgroundJobResult(EventMessage basicMessage)
        {
            Headers = basicMessage.Headers;
            BodyText = basicMessage.BodyText;
        }

        /// <summary>
        /// Gets the Unique Id of the Job
        /// </summary>
        public string JobUUID
        {
            get
            {
                return Headers[HeaderNames.JobUUID];
            }
        }

        /// <summary>
        /// Gets a boolean indicating whether the job succeeded or not.
        /// </summary>
        public bool Success
        {
            get
            {
                return BodyText != null && BodyText[0] == '+';
            }
        }

        /// <summary>
        /// Gets the error message associated with a failed bgapi call.
        /// </summary>
        public string ErrorMessage
        {
            get
            {
                return BodyText != null && BodyText.StartsWith("-ERR ")
                           ? BodyText[5..]
                           : BodyText;
            }
        }
    }
}
