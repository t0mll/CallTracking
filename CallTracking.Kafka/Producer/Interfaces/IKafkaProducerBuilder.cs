using Confluent.Kafka;

namespace CallTracking.Kafka.Common.Producer.Interfaces
{
    public interface IKafkaProducerBuilder
    {
        IProducer<string, string> Build();
    }
}
