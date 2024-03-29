﻿using Microsoft.Extensions.DependencyInjection;
using System;
using MediatR;
using CallTracking.Kafka.Common.Consumer.Interfaces;

namespace CallTracking.Kafka.Common.Consumer.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddKafkaConsumer(this IServiceCollection services,
            params Type[] handlerAssemblyMarkerTypes)
        {
            services.AddMediatR(handlerAssemblyMarkerTypes);

            services.AddTransient<IKafkaMessageConsumerManager>(serviceProvider =>
                new KafkaMessageConsumerManager(serviceProvider, services));

            services.AddTransient<IKafkaConsumerBuilder, KafkaConsumerBuilder>();

            services.AddTransient<IKafkaTopicMessageConsumer, KafkaTopicMessageConsumer>();

            return services;
        }
    }
}
