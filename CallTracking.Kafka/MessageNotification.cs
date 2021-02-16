using CallTracking.Kafka.Common.Interfaces;
using MediatR;

namespace CallTracking.Kafka.Common
{
    public class MessageNotification<TMessage> : INotification
        where TMessage : IMessage
    {
        public MessageNotification(TMessage message)
        {
            Message = message;
        }

        public TMessage Message { get; }
    }
}
