using CallTraking.NEventSocket.Common.FreeSWITCH.Channel;
using CallTraking.NEventSocket.Common.FreeSWITCH.Events;
using CallTraking.NEventSocket.Common.FreeSWITCH.Messages;
using System;

namespace CallTraking.NEventSocket.Common.FreeSWITCH.Applications.AttendedTransfer
{
    /// <summary>
    /// Represents the result of an Attended Transfer
    /// </summary>
    public class AttendedTransferResult
    {
        protected AttendedTransferResult()
        {
        }

        /// <summary>
        /// Gets the <seealso cref="AttendedTransferResultStatus"/> of the application.
        /// </summary>
        public AttendedTransferResultStatus Status { get; private set; }

        /// <summary>
        /// Gets the hangup cause of the C-Leg.
        /// </summary>
        public HangupCause? HangupCause { get; private set; }

        protected internal static AttendedTransferResult Hangup(ChannelEvent hangupMessage)
        {
            if (hangupMessage.EventName != EventNames.ChannelHangup)
            {
                throw new InvalidOperationException($"Expected event of type ChannelHangup, got {hangupMessage.EventName} instead");
            }

            return new AttendedTransferResult() { HangupCause = hangupMessage.HangupCause, Status = AttendedTransferResultStatus.Failed };
        }

        protected internal static AttendedTransferResult Success(
            AttendedTransferResultStatus status = AttendedTransferResultStatus.Transferred)
        {
            return new AttendedTransferResult() { Status = status };
        }

        protected internal static AttendedTransferResult Aborted()
        {
            return new AttendedTransferResult() { Status = AttendedTransferResultStatus.Aborted };
        }

        protected internal static AttendedTransferResult Failed(HangupCause hangupCause)
        {
            return new AttendedTransferResult() { Status = AttendedTransferResultStatus.Failed, HangupCause = hangupCause };
        }
    }
}
