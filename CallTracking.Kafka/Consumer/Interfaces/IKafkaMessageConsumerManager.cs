using System.Threading;

namespace CallTracking.Kafka.Consumer.Interfaces
{
    public interface IKafkaMessageConsumerManager
    {
        void StartConsumers(CancellationToken cancellationToken);
    }
}
