using CallTracking.Kafka.Common.Consumer.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CallTracking.FSConsumerWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IKafkaMessageConsumerManager _kafkaMessageConsumerManager;

        public Worker(ILogger<Worker> logger, IKafkaMessageConsumerManager kafkaMessageConsumerManager)
        {
            _logger = logger;
            _kafkaMessageConsumerManager = kafkaMessageConsumerManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _kafkaMessageConsumerManager.StartConsumers(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
