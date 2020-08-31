using MessageBrokers.MessageFormats;
using MessageBrokers.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MessageBrokers
{
    public interface IMessageBroker
    {
        Task EnqueueAsync<T>(T message, CancellationToken cancellationToken) where T : class, IMessage;
        Task ProcessMessagesAsync<T>(SubscriptionInfo<T> subscriptionInfo, CancellationToken cancellationToken) where T: class, IMessage;
    }
}
