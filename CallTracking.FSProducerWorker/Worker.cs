using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NEventSocket;
using NEventSocket.FreeSwitch;
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
        public Worker(IConfiguration configuration, ILogger<Worker> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var hostname = _configuration["FREESWITCH_ESL_HOSTNAME"];
            var port = int.Parse(_configuration["FREESWITCH_ESL_PORT"]);

            while (!stoppingToken.IsCancellationRequested)
            {
                using (var socket = await InboundSocket.Connect(hostname, port, "ClueCon"))
                {
                    var apiResponse = await socket.SendApi("status");
                    _logger.LogInformation(apiResponse.BodyText);

                    // Tell FreeSwitch which events we are interested in
                    await socket.SubscribeEvents(EventName.ChannelAnswer);

                    // Handle events as they come in using Rx
                    socket.ChannelEvents.Where(x => x.EventName == EventName.ChannelAnswer)
                          .Subscribe(async x =>
                          {
                              _logger.LogInformation("Channel Answer Event " + x.UUID);

                              // We have a channel id, now we can control it
                              await socket.Play(x.UUID, "misc/8000/misc-freeswitch_is_state_of_the_art.wav");
                          });
                }
            }
        }
    }
}