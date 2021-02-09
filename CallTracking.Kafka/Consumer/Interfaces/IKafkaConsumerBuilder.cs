using Confluent.Kafka;

namespace CallTracking.Kafka.Consumer.Interfaces
{
    public interface IKafkaConsumerBuilder
    {
        IConsumer<string, string> Build();
    }
}
