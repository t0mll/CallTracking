using System.Threading;

namespace CallTracking.Kafka.Consumer.Interfaces
{
    public interface IKafkaTopicMessageConsumer
    {
        void StartConsuming(string topic, CancellationToken cancellationToken);
    }
}
