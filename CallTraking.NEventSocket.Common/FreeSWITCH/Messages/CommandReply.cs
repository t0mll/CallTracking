using CallTraking.NEventSocket.Common.FreeSWITCH.Headers;
using System;
using System.Collections.Generic;
using System.Text;

namespace CallTraking.NEventSocket.Common.FreeSWITCH.Messages
{
    /// <summary>
    /// Represents the result of an ESL command
    /// </summary>
    [Serializable]
    public class CommandReply : BasicMessage
    {
        internal CommandReply(BasicMessage basicMessage)
        {
            if (basicMessage.ContentType != ContentTypes.CommandReply)
            {
                throw new ArgumentException($"Expected content type command/reply, got {basicMessage.ContentType} instead.");
            }

            Headers = basicMessage.Headers;
            BodyText = basicMessage.BodyText;
        }

        /// <summary>
        /// Gets a boolean indicating whether the command succeeded.
        /// </summary>
        public bool Success
        {
            get
            {
                return ReplyText != null && ReplyText[0] == '+';
            }
        }

        /// <summary>
        /// Gets the reply text
        /// </summary>
        public string ReplyText
        {
            get
            {
                return Headers[HeaderNames.ReplyText];
            }
        }

        /// <summary>
        /// Gets an error message associated with a failed command
        /// </summary>
        public string ErrorMessage
        {
            get
            {
                return ReplyText != null && ReplyText.StartsWith("-ERR")
                           ? ReplyText[5..]
                           : string.Empty;
            }
        }
    }
}