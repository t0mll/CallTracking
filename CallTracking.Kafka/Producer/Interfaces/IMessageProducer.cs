using CallTracking.Kafka.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace CallTracking.Kafka.Producer.Interfaces
{
    public interface IMessageProducer
    {
        Task ProduceAsync(string key, IMessage message, CancellationToken cancellationToken);
    }
}
