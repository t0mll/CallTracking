using CallTracking.Kafka.Common.Producer.Interfaces;
using CallTracking.Kafka.Messaging.Messages;
using CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Originate;
using CallTraking.NEventSocket.Common.FreeSWITCH.Events;
using CallTraking.NEventSocket.Common.Sockets.Interfaces;
using CallTraking.NEventSocket.Common.Utils.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CallTracking.FSProducerWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IEventSocket _socket;
        private readonly IMessageProducer _messageProducer;

        public Worker(ILogger<Worker> logger, IConfiguration configuration, IEventSocket socket, IMessageProducer messageProducer)
        {
            _logger = logger;
            _configuration = configuration;
            _socket = socket;
            _messageProducer = messageProducer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var apiResponse = await _socket.SendApi("status");
            _logger.LogInformation(apiResponse.BodyText);

            await _socket.SubscribeEvents(EventNames.All);

            _socket.ChannelEvents.Subscribe(e => _logger.LogInformation($"Channel Event [{e.UUID}] - {e.EventName}"));

            _socket.ChannelEvents.Where(x => x.EventName == EventNames.ChannelAnswer)
                .Subscribe(async x => await _messageProducer.ProduceAsync(x.UUID, new ChannelAnswerEventMessage(x.UUID, x.BodyText,x.ChannelState, x.AnswerState, x.Headers), stoppingToken));

            var originate = await _socket.Originate(
                            "user/1000",
                            new OriginateOptions
                            {
                                CallerIdNumber = "123456789",
                                CallerIdName = "Test call",
                                HangupAfterBridge = false,
                                TimeoutSeconds = 20,
                            });

            if (!originate.Success)
            {
                _logger.LogError($"Failed to originate the call {originate.HangupCause}");
            }
        }
    }
}