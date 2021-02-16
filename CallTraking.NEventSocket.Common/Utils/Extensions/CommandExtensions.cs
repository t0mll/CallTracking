using CallTraking.NEventSocket.Common.FreeSWITCH.Channel;
using CallTraking.NEventSocket.Common.FreeSWITCH.Events;
using CallTraking.NEventSocket.Common.FreeSWITCH.Messages;
using CallTraking.NEventSocket.Common.Sockets;
using CallTraking.NEventSocket.Common.Utils.ObjectPooling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallTraking.NEventSocket.Common.Utils.Extensions
{
    /// <summary>
    /// Defines ESL operations that operate on either an <seealso cref="InboundSocket"/> or an <seealso cref="OutboundSocket"/>.
    /// </summary>
    public static class CommandExtensions
    {
        public static Task<CommandReply> DivertEventsOn(this EventSocket eventSocket)
        {
            return eventSocket.SendCommand("divert_events on");
        }

        public static Task<CommandReply> DivertEventsOff(this EventSocket eventSocket)
        {
            return eventSocket.SendCommand("divert_events off");
        }

        public static Task<CommandReply> Filter(this EventSocket eventSocket, EventNames eventName)
        {
            return eventSocket.Filter(eventName.ToString().ToUpperWithUnderscores());
        }

        public static Task<CommandReply> Filter(this EventSocket eventSocket, string eventName)
        {
            return eventSocket.Filter("Event-Name", eventName);
        }

        public static Task<CommandReply> Filter(this EventSocket eventSocket, string header, string value)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return eventSocket.SendCommand($"filter {header} {value}");
        }

        public static Task<CommandReply> FilterDelete(this EventSocket eventSocket, EventNames eventName)
        {
            return eventSocket.FilterDelete("Event-Name", eventName.ToString().ToUpperWithUnderscores());
        }

        public static Task<CommandReply> FilterDelete(this EventSocket eventSocket, string header)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            return eventSocket.SendCommand($"filter delete {header}");
        }

        public static Task<CommandReply> FilterDelete(this EventSocket eventSocket, string header, string value)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return eventSocket.SendCommand($"filter delete {header} {value}");
        }

        public static Task<CommandReply> SendEvent(
            this EventSocket eventSocket, EventNames eventName, IDictionary<string, string> headers = null)
        {
            return SendEvent(eventSocket, eventName.ToString().ToUpperWithUnderscores(), headers);
        }

        public static Task<CommandReply> SendEvent(
            this EventSocket eventSocket, string eventName, IDictionary<string, string> headers = null)
        {
            if (eventName == null)
            {
                throw new ArgumentNullException(nameof(eventName));
            }

            if (headers == null)
            {
                headers = new Dictionary<string, string>();
            }

            var headersString = headers.Aggregate(
                StringBuilderPool.Allocate(),
                (sb, kvp) =>
                {
                    sb.AppendFormat("{0}: {1}", kvp.Key, kvp.Value);
                    sb.Append("\n");
                    return sb;
                },
                StringBuilderPool.ReturnAndFree);

            return eventSocket.SendCommand($"sendevent {eventName}\n{headersString}");
        }

        public static Task<CommandReply> Hangup(
            this EventSocket eventSocket, string uuid, HangupCause hangupCause = HangupCause.NormalClearing)
        {
            if (uuid == null)
            {
                throw new ArgumentNullException(nameof(uuid));
            }

            return
                eventSocket.SendCommand($"sendmsg {uuid}\ncall-command: hangup\nhangup-cause: {hangupCause.ToString().ToUpperWithUnderscores()}");
        }

        public static Task<CommandReply> FsLog(this EventSocket eventSocket, string logLevel)
        {
            return eventSocket.SendCommand("log " + logLevel);
        }

        public static Task<CommandReply> NoLog(this EventSocket eventSocket)
        {
            return eventSocket.SendCommand("nolog");
        }

        public static Task<CommandReply> NoEvents(this EventSocket eventSocket)
        {
            return eventSocket.SendCommand("noevents");
        }
    }
}
