using CallTracking.Kafka.Common;
using CallTracking.Kafka.Messaging.Messages;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace CallTracking.FSConsumerWorker.Handlers
{
    public class ChannelAnswerEventMessageLoggerHandler : INotificationHandler<MessageNotification<ChannelAnswerEventMessage>>
    {
        private readonly ILogger<ChannelAnswerEventMessageLoggerHandler> _logger;

        public ChannelAnswerEventMessageLoggerHandler(ILogger<ChannelAnswerEventMessageLoggerHandler> logger)
        {
            _logger = logger;
        }
        public Task Handle(MessageNotification<ChannelAnswerEventMessage> notification, CancellationToken cancellationToken)
        {
            var message = notification.Message;

            _logger.LogInformation(
                $"Channel Answer event message received with ChannelId: {message.ChannelId} and ChannelState: {message.ChannelState}");

            return Task.CompletedTask;
        }
    }
}
