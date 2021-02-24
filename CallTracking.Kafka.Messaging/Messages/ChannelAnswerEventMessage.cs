using CallTracking.Kafka.Common;
using CallTracking.Kafka.Common.Interfaces;
using CallTraking.NEventSocket.Common.FreeSWITCH.Channel;
using System.Collections.Generic;

namespace CallTracking.Kafka.Messaging.Messages
{
    [MessageTopic("channelevent-answer-messages")]
    public class ChannelAnswerEventMessage : IMessage
    {
        public ChannelAnswerEventMessage(string channelId, string bodyText, ChannelState? channelState, AnswerState? answerState, IDictionary<string, string> headers)
        {
            ChannelId = channelId;
            BodyText = bodyText;
            ChannelState = channelState;
            AnswerState = answerState;
            Headers = headers;
        }

        public string ChannelId { get; set; }
        public string BodyText { get; set; }
        public ChannelState? ChannelState { get; set; }
        public AnswerState? AnswerState { get; set; }
        public IDictionary<string, string> Headers { get; set; }
    }
}
