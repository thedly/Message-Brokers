
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using global::MessageBrokers.Utilities;
using MessageBrokers.MessageFormats;
using MessageBrokers.Options;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MessageBrokers
{


    public class ActiveMQBroker : IMessageBroker
    {
        private const string ConnectionUnavailable = "Connection unavailable";

        private readonly ISerializationManager serializationManager;
        private readonly MessageBrokerOptions messageBrokerOptions;
        private readonly ConnectionFactory connectionFactory;
        private IConnection _connection;

        private readonly object syncLock;


        public ActiveMQBroker(IOptions<MessageBrokerOptions> options)
        {
            this.syncLock = new object();
            this.serializationManager = new JsonSerializationManager();
            this.messageBrokerOptions = options.Value;
            this.serializationManager = new JsonSerializationManager();
            this.connectionFactory = new ConnectionFactory();
            this.connectionFactory.UserName = this.messageBrokerOptions.MessageBrokerUsername;
            this.connectionFactory.Password = this.messageBrokerOptions.MessageBrokerPassword;
            this.connectionFactory.BrokerUri = new Uri($"{this.messageBrokerOptions.MessageBrokerHost}");
        }

        public Task EnqueueAsync<T>(T message, CancellationToken cancellationToken) where T : class, Options.IMessage
        {
            if (!IsConnected)
            {
                TryConnect();
            }

            using (var channel = _connection.CreateSession())
            {

                IDestination dest = channel.GetQueue(message.QName);
                using (IMessageProducer producer = channel.CreateProducer(dest))
                {
                    producer.DeliveryMode = MsgDeliveryMode.Persistent;
                    string json = JsonConvert.SerializeObject(message, Formatting.Indented);
                    var txtMessage = channel.CreateTextMessage(json);
                    var objectMessage = producer.CreateObjectMessage(json);
                    producer.Send(txtMessage);
                }
            }
            return Task.CompletedTask;
        }

        public async Task ProcessMessagesAsync<T>(SubscriptionInfo<T> subscriptionInfo, CancellationToken cancellationToken) where T : class, Options.IMessage
        {
            if (!IsConnected)
            {
                TryConnect();
            }

            var tasks = new List<Task>();
            using (var _session = _connection.CreateSession())
            {
                var batch = subscriptionInfo.Consumer.Batch;
                for (int i = 0; i < batch; i++)
                {
                    ConcurrentQueue<ITextMessage> queue = new ConcurrentQueue<ITextMessage>();
                    IDestination dest = _session.GetQueue(subscriptionInfo.Consumer.QName);
                    IMessageConsumer consumer = _session.CreateConsumer(dest);
                    {

                        tasks.Add(SubscribeAsync(consumer, queue));

                        tasks.Add(StartWorkerAsync<T>(consumer, subscriptionInfo, queue, cancellationToken));
                    }
                }
                await Task.WhenAll(tasks);
            }
        }

        private Task SubscribeAsync(IMessageConsumer consumer, ConcurrentQueue<ITextMessage> queue)
        {

            consumer.Listener += (eventArgs) =>
            {
                var messageString = eventArgs as ITextMessage;
                queue.Enqueue(messageString);
                    //eventArgs.Acknowledge();
                };

            return Task.CompletedTask;
        }


        private async Task StartWorkerAsync<T>(IMessageConsumer consumer, SubscriptionInfo<T> subscriptionInfo, ConcurrentQueue<ITextMessage> queue, CancellationToken cancellationToken) where T: class, Options.IMessage
        {
            while (!cancellationToken.IsCancellationRequested)
            {

                ITextMessage messageInfo;

                //messageInfo = consumer.Receive(TimeSpan.FromMilliseconds(2000));
                var result = queue.TryDequeue(out messageInfo);
                if (messageInfo != null)
                {

                    var jsonMessage = messageInfo.Text;
                    var message = serializationManager.Deserialize<T>(jsonMessage);
                    try
                    {
                        await subscriptionInfo.OnReceivedAsync?.Invoke(message, cancellationToken);
                        messageInfo.Acknowledge();
                    }
                    catch (Exception ex)
                    {
                        await subscriptionInfo?.OnErrorAsync?.Invoke(message, ex, cancellationToken);
                    }
                }
                await Task.Delay(1000, cancellationToken);
            }
        }




        private void TryConnect()
        {
            lock (this.syncLock)
            {
                try
                {
                    if (IsConnected)
                    {
                        return;
                    }

                    this._connection = connectionFactory.CreateConnection();
                    this._connection.Start();
                    if (!IsConnected)
                    {
                        throw new MessageBrokerException(ConnectionUnavailable);
                    }
                }
                catch (ConnectionFailedException ex)
                {
                    throw new MessageBrokerException(ex.Message);
                }
                catch (Exception ex)
                {
                    while (ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                    }

                    throw new MessageBrokerException(ex.Message);
                }
            }


        }

        public bool IsConnected
        {
            get
            {
                return this._connection != null && this._connection.IsStarted;
            }
        }

    }
}


