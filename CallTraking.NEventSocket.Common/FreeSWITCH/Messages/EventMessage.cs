using CallTraking.NEventSocket.Common.FreeSWITCH.Events;
using CallTraking.NEventSocket.Common.FreeSWITCH.Headers;
using CallTraking.NEventSocket.Common.Utils.Extensions;
using CallTraking.NEventSocket.Common.Utils.ObjectPooling;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CallTraking.NEventSocket.Common.FreeSWITCH.Messages
{
    /// <summary>
    ///     Represents an Event Message received through the EventSocket
    /// </summary>
    [Serializable]
    public class EventMessage : BasicMessage
    {
        private readonly ILogger<EventMessage> _logger;

        internal EventMessage(BasicMessage basicMessage)
        {
            if (basicMessage is EventMessage)
            {
                Headers = basicMessage.Headers;
                BodyText = basicMessage.BodyText;
                return;
            }

            if (basicMessage.ContentType == ContentTypes.CommandReply)
            {
                /* 
                 * Special Case:
                 * When an Outbound Socket sends the "connect" command, FreeSwitch responds with a Command-Reply.
                 * This Command-Reply message contains a CHANNEL_DATA event message buried in its headers.
                 * In this case, we can hydrate an event message from a message of content type Command-Reply.
                 */
                if (basicMessage.Headers.ContainsKey(HeaderNames.EventName))
                {
                    Headers = basicMessage.Headers;
                    BodyText = basicMessage.BodyText;
                    return;
                }

                /* otherwise, we'll throw an exception if we get the wrong content-type message passed in*/
                throw new InvalidOperationException($"Expected content type event/plain, got {basicMessage.ContentType} instead.");
            }

            // normally, the content of the event will be in the BasicMessage's body text and will need to be parsed to produce an EventMessage
            if (string.IsNullOrEmpty(basicMessage.BodyText))
            {
                throw new ArgumentException("Message did not contain an event body.");
            }

            try
            {
                var delimiterIndex = basicMessage.BodyText.IndexOf("\n\n", StringComparison.Ordinal);
                if (delimiterIndex == -1 || delimiterIndex == basicMessage.BodyText.Length - 2)
                {
                    // body text consists of key-value-pair event headers, no body
                    Headers = basicMessage.BodyText.ParseKeyValuePairs(": ");
                    BodyText = null;
                }
                else
                {
                    // ...but some Event Messages also carry a body payload, eg. a BACKGROUND_JOB event
                    // which is a message carried inside an EventMessage carried inside a BasicMessage..
                    Headers = basicMessage.BodyText.Substring(0, delimiterIndex).ParseKeyValuePairs(": ");

                    Debug.Assert(Headers.ContainsKey(HeaderNames.ContentLength));
                    var contentLength = int.Parse(Headers[HeaderNames.ContentLength]);

                    Debug.Assert(delimiterIndex + 2 + contentLength <= basicMessage.BodyText.Length, "Message cut off mid-transmission");
                    var body = basicMessage.BodyText.Substring(delimiterIndex + 2, contentLength);

                    //remove any \n\n if any
                    var index = body.IndexOf("\n\n", StringComparison.Ordinal);
                    BodyText = index > 0 ? body.Substring(0, index) : body;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse body of event");
                _logger.LogError(BodyText);
                throw;
            }

        }

        /// <summary>
        /// Default constructor
        /// </summary>
        protected EventMessage(ILogger<EventMessage> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets the <see cref="EventName"/> of this instance.
        /// </summary>
        public EventNames EventName
        {
            get
            {
                return Headers.GetValueOrDefault(HeaderNames.EventName).HeaderToEnum<EventNames>();
            }
        }

        /// <summary>
        /// Retrieves a header from the Headers dictionary, returning null if the key is not found.
        /// </summary>
        /// <param name="header">The Header Name.</param>
        /// <returns>The Header Value.</returns>
        public string GetHeader(string header)
        {
            return Headers.GetValueOrDefault(header);
        }

        /// <summary>
        /// Retrieves a Channel Variable from the Headers dictionary, returning null if the key is not found.
        /// </summary>
        /// <param name="variable">The Channel Variable Name</param>
        /// <returns>The Channel Variable value.</returns>
        public string GetVariable(string variable)
        {
            return GetHeader("variable_" + variable);
        }

        /// <summary>
        /// Provides a string representation of the <see cref="EventMessage"/> instance for debugging.
        /// </summary>
        public override string ToString()
        {
            var sb = StringBuilderPool.Allocate();
            sb.AppendLine("Event Headers:");

            foreach (var h in Headers.OrderBy(x => x.Key))
            {
                sb.AppendLine("\t" + h.Key + " : " + h.Value);
            }

            if (!string.IsNullOrEmpty(BodyText))
            {
                sb.AppendLine("Body:");
                sb.AppendLine(BodyText);
            }

            return StringBuilderPool.ReturnAndFree(sb);
        }
    }
}
