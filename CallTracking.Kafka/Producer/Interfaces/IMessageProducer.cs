using CallTracking.Kafka.Common.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace CallTracking.Kafka.Common.Producer.Interfaces
{
    public interface IMessageProducer
    {
        Task ProduceAsync(string key, IMessage message, CancellationToken cancellationToken);
    }
}
