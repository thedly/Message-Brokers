using MessageBrokers.MessageFormats;
using MessageBrokers.Options;
using MessageBrokers.Utilities;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MessageBrokers
{
    public class RabbitMQBroker : IMessageBroker
    {
        private const string ConnectionUnavailable = "Connection unavailable";
        private readonly MessageBrokerOptions messageBrokerOptions;
        private readonly ISerializationManager serializationManager;
        private readonly ConnectionFactory connectionFactory;
        private readonly Dictionary<string, IModel> channels;
        private readonly object syncLock;
        private IConnection connection;

        public RabbitMQBroker(IOptions<MessageBrokerOptions> options)
        {
            
            this.syncLock = new object();
            this.messageBrokerOptions = options.Value;
            this.serializationManager = new JsonSerializationManager();
            channels = new Dictionary<string, IModel>();

            this.connectionFactory = new ConnectionFactory();
            this.connectionFactory.AutomaticRecoveryEnabled = true;
            this.connectionFactory.UserName = this.messageBrokerOptions.MessageBrokerUsername;
            this.connectionFactory.Password = this.messageBrokerOptions.MessageBrokerPassword;
            this.connectionFactory.HostName = this.messageBrokerOptions.MessageBrokerHost;

        }

        
        public Task EnqueueAsync<T>(T message, CancellationToken cancellationToken) where T : class, IMessage
        {
            if (!IsConnected)
            {
                TryConnect();
            }

            var channel = GetChannel(message);
            var str = this.serializationManager.Serialize(message);
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            channel.BasicPublish(message.Exchange, message.Route, properties, Encoding.UTF8.GetBytes(str));
            return Task.CompletedTask;
        }

        public bool IsConnected
        {
            get
            {
                return this.connection != null && this.connection.IsOpen;
            }
        }

        //public async Task<> getmessage<T>(SubscriptionInfo<T> subscriptionInfo, CancellationToken cancellationToken) where T: class, IMessage
        //{
        //    if (!IsConnected)
        //    {
        //        TryConnect();
        //    }

        //    var channel = GetChannel(subscriptionInfo.Consumer);

            
        //    channel.getMessageCount();
        //}

        public async Task ProcessMessagesAsync<T>(SubscriptionInfo<T> subscriptionInfo, CancellationToken cancellationToken)
            where T : class, IMessage
        {
            if (!IsConnected)
            {
                TryConnect();
            }

            var channel = GetChannel(subscriptionInfo.Consumer);
            var batch = subscriptionInfo.Consumer.Batch;

            channel.BasicQos(0, batch, false);

            var tasks = new List<Task>();
            var queue = new ConcurrentQueue<MessageInfo>();
            await SubscribeAsync(channel, queue, subscriptionInfo.Consumer.QName);
            for (int i = 0; i < batch; i++)
            {
                tasks.Add(StartWorkerAsync(channel, queue, subscriptionInfo, cancellationToken));
            }

            await Task.WhenAll(tasks);

            // close channel
            channel.Close();

        }

        private IModel GetChannel<T>(T message) where T : IMessage
        {
            
            lock (syncLock)
            {
                var channelExists = channels.Any(x => x.Key == message.QName);
                if (!channelExists)
                {
                    var ch = connection.CreateModel();

                    ch.ExchangeDeclare(message.Exchange, ExchangeType.Direct, durable: true, autoDelete: false, arguments: null);
                    ch.QueueDeclare(message.QName, true, false, false);
                    ch.QueueBind(message.QName, message.Exchange, message.Route);

                    channels.Add(message.QName, ch);
                }

                return channels.FirstOrDefault(x => x.Key.Equals(message.QName)).Value;
            }
            
        }

        private Task SubscribeAsync(IModel channel, ConcurrentQueue<MessageInfo> queue, string queueName)
        {
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, eventArgs) =>
            {
                var messageString = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
                var message = new MessageInfo()
                {
                    Data = messageString,
                    DeliveryTag = eventArgs.DeliveryTag
                };

                queue.Enqueue(message);
            };

            channel.BasicConsume(queueName, false, consumer);
            return Task.CompletedTask;
        }

        private async Task StartWorkerAsync<T>(IModel channel, ConcurrentQueue<MessageInfo> queue, SubscriptionInfo<T> subscriptionInfo, CancellationToken cancellationToken)
            where T : class, IMessage
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                MessageInfo messageInfo;
                var result = queue.TryDequeue(out messageInfo);
                if (messageInfo != null)
                {
                    // expand before deserializion
                    //var expandedData = messageInfo.Data.Expand();



                    T message = default(T);

                    try
                    {
                        message = this.serializationManager.Deserialize<T>(messageInfo.Data);
                        await subscriptionInfo.OnReceivedAsync?.Invoke(message, cancellationToken);
                        channel.BasicAck(messageInfo.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        
                        channel.BasicNack(messageInfo.DeliveryTag, false, true);
                        

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

                    this.connection = connectionFactory.CreateConnection();
                    if (!IsConnected)
                    {
                        throw new MessageBrokerException(ConnectionUnavailable);
                    }
                }
                catch (BrokerUnreachableException ex)
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
    }
}
