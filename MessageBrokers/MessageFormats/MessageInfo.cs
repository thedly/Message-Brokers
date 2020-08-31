namespace MessageBrokers.MessageFormats
{
    public class MessageInfo
    {
        public string Data { get; set; }

        public ulong DeliveryTag { get; set; }
    }
}