using CallTracking.Kafka.Common.Producer.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CallTracking.Kafka.Common.Producer.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddKafkaProducer(this IServiceCollection services)
        {
            services.AddSingleton<IKafkaProducerBuilder, KafkaProducerBuilder>();

            services.AddSingleton<IMessageProducer, KafkaMessageProducer>();

            return services;
        }
    }
}
