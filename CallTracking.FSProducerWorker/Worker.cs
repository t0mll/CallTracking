using CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Originate;
using CallTraking.NEventSocket.Common.FreeSWITCH.Events;
using CallTraking.NEventSocket.Common.FreeSWITCH.Headers;
using CallTraking.NEventSocket.Common.Sockets;
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

        public Worker(ILogger<Worker> logger, IConfiguration configuration, IEventSocket socket)
        {
            _logger = logger;
            _configuration = configuration;
            _socket = socket;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var apiResponse = await _socket.SendApi("status");
            _logger.LogInformation(apiResponse.BodyText);

            var originate =
                    await
                        _socket.Originate(

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
            else
            {
                var uuid = originate.ChannelData.UUID;
                _logger.LogInformation($"Channel [{uuid}]");

                await _socket.SubscribeEvents(EventNames.All);  

                _socket.OnHangup(
                    uuid,
                    e =>
                    {
                        _logger.LogWarning($"Hangup Detected on A-Leg {e.Headers[HeaderNames.CallerUniqueId]} {e.Headers[HeaderNames.HangupCause]}");

                        _socket.Exit();
                    });

                _socket.ChannelEvents.Where(x =>
                    x.UUID == uuid &&
                    x.EventName == EventNames.Dtmf).Subscribe(
                    e =>
                    {
                        _logger.LogInformation("Got DTMF");
                        _logger.LogInformation($"{ e.UUID == uuid}");
                        _logger.LogInformation($"UIIDS: event {e.UUID} ours {uuid}");
                        _logger.LogInformation(e.Headers[HeaderNames.DtmfDigit]);
                    });

            }

            while (!stoppingToken.IsCancellationRequested)
            {


            }
        }
    }
}