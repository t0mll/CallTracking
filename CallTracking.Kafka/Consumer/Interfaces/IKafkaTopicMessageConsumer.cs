using System.Threading;

namespace CallTracking.Kafka.Common.Consumer.Interfaces
{
    public interface IKafkaTopicMessageConsumer
    {
        void StartConsuming(string topic, CancellationToken cancellationToken);
    }
}
