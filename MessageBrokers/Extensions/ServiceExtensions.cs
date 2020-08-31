using MessageBrokers.Options;
using MessageBrokers.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace MessageBrokers.Extensions
{
    public static class ServiceExtensions
    {
        public static void AddMessageBrokerDependencies(this IServiceCollection services, MessageBrokerTypes broker)
        {

            services.AddSingleton<ISerializationManager, JsonSerializationManager>();

            switch (broker)
            {
                case MessageBrokerTypes.RabbitMQ:
                    services.AddScoped<IMessageBroker, RabbitMQBroker>();
                    break;
                case MessageBrokerTypes.ActiveMQ:
                    services.AddScoped<IMessageBroker, ActiveMQBroker>();
                    break;
                case MessageBrokerTypes.SQS:
                    services.AddScoped<IMessageBroker, SQSBroker>();
                    break;
            }
        }

        public static void AddMessageBrokerDependencies(this IServiceCollection services, MessageBrokerOptions )
        {

        }

    }
}
