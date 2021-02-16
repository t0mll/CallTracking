using CallTraking.NEventSocket.Common.FreeSWITCH.Headers;
using CallTraking.NEventSocket.Common.Utils.Extensions;
using CallTraking.NEventSocket.Common.Utils.ObjectPooling;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CallTraking.NEventSocket.Common.FreeSWITCH.Messages
{
    /// <summary>
    /// All messages received through the EventSocket start off as a Basic Message.
    /// Some messages are embedded inside other messages, eg Events with bodies containing BgApi results.
    /// </summary>
    [Serializable]
    public class BasicMessage
    {
        internal BasicMessage(IDictionary<string, string> headers)
        {
            Headers = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
        }

        internal BasicMessage(IDictionary<string, string> headers, string body)
        {
            Headers = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
            BodyText = body;
        }

        /// <summary>
        /// Default constructor allows child classes to instantiate.
        /// </summary>
        protected BasicMessage()
        {
        }

        /// <summary>
        /// Gets the Headers of the Message.
        /// </summary>
        public IDictionary<string, string> Headers { get; protected set; }

        /// <summary>
        /// Gets any body text, if any, contained in the Message.
        /// </summary>
        public string BodyText { get; protected set; }

        /// <summary>
        /// Gets the Content Type header.
        /// </summary>
        public string ContentType
        {
            get
            {
                return Headers.GetValueOrDefault(HeaderNames.ContentType);
            }
        }

        /// <summary>
        /// Gets the content length if specified by the Content-Length header.
        /// </summary>
        public int ContentLength
        {
            get
            {
                int contentLength;
                return int.TryParse(Headers.GetValueOrDefault(HeaderNames.ContentLength), out contentLength) ? contentLength : 0;
            }
        }

        /// <summary>ToString helper.</summary>
        /// <returns>A <see cref="string"/> representation of the BasicMessage instance.</returns>
        public override string ToString()
        {
            var sb = StringBuilderPool.Allocate();
            sb.AppendLine("Headers:\n");

            foreach (var h in Headers.OrderBy(x => x.Key))
            {
                sb.AppendFormat($"\t{h.Key}:{h.Value}\n");
            }

            if (BodyText != null)
            {
                sb.AppendLine("Body:\n");
                sb.Append("\t");
                sb.AppendLine(BodyText);
            }

            return StringBuilderPool.ReturnAndFree(sb);
        }
    }
}
