using MessageBrokers.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MessageBrokers.MessageFormats
{
    public class SubscriptionInfo<T> where T: class, IMessage
    {
        public SubscriptionInfo(T model)
        {
            Consumer = model;
        }
        public T Consumer { get; set; }
        public Func<T, CancellationToken, Task> OnReceivedAsync { get; set; }
        public Func<T, Exception, CancellationToken, Task> OnErrorAsync { get; set; }
    }
}
