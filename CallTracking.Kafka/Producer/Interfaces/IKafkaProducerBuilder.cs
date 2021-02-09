using Confluent.Kafka;

namespace CallTracking.Kafka.Producer.Interfaces
{
    public interface IKafkaProducerBuilder
    {
        IProducer<string, string> Build();
    }
}
