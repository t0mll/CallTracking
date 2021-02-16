using System.Threading;

namespace CallTracking.Kafka.Common.Consumer.Interfaces
{
    public interface IKafkaMessageConsumerManager
    {
        void StartConsumers(CancellationToken cancellationToken);
    }
}
