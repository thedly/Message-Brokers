using MessageBrokers.MessageFormats;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using MessageBrokers.Utilities;
using MessageBrokers.Options;
using System.Linq;
using Amazon;

namespace MessageBrokers
{
    public class SQSBroker : IMessageBroker
    {
        private readonly AmazonSQSClient _sqsClient;
        private readonly ISerializationManager _serializationManager;
        public SQSBroker(
                ISerializationManager serializationManager
            )
        {
            _serializationManager = serializationManager;

            var config = new AmazonSQSConfig { ServiceURL = "http://localhost:9324" };

            _sqsClient = new AmazonSQSClient(config);
        }


        public async Task EnqueueAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken) where T : class, IMessage
        {


            var group = messages.Select(x => new SendMessageBatchRequestEntry { MessageGroupId = x.QName, Id = Guid.NewGuid().ToString(), MessageBody = _serializationManager.Serialize(x) }).ToList();

            await _sqsClient.SendMessageBatchAsync(new SendMessageBatchRequest
            {
                Entries = group,
                QueueUrl = "http://localhost:9324/queue/default",
                
            });
        }

        public async Task EnqueueAsync<T>(T message, CancellationToken cancellationToken) where T : class, IMessage
        {
            await _sqsClient.SendMessageAsync(new SendMessageRequest()
            {
                QueueUrl = "http://localhost:9324/queue/default",
                MessageBody = _serializationManager.Serialize(message)
            });            
        }

        public async Task ProcessMessagesAsync<T>(SubscriptionInfo<T> subscriptionInfo, CancellationToken cancellationToken) where T : class, IMessage
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var messages = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        MaxNumberOfMessages = 2,
                        QueueUrl = "http://localhost:9324/queue/default"
                    });

                    var data = messages.Messages;


                    foreach(var item in data)
                    {
                        var message = _serializationManager.Deserialize<T>(item.Body);
                        await subscriptionInfo.OnReceivedAsync?.Invoke(message, cancellationToken);

                        await _sqsClient.DeleteMessageAsync(new DeleteMessageRequest { QueueUrl = "http://localhost:9324/queue/default", ReceiptHandle = item.ReceiptHandle });


                    }


                    
                }
                catch (Exception ex)
                {

                    throw;
                }

                await Task.Delay(1000);
                
            }
        }
    }
}
