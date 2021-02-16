using Confluent.Kafka;

namespace CallTracking.Kafka.Common.Consumer.Interfaces
{
    public interface IKafkaConsumerBuilder
    {
        IConsumer<string, string> Build();
    }
}
