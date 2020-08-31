using MessageBrokers.MessageFormats;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MessageBrokers.Options
{
    public class MessageBrokerOptions
    {
        public string MessageBrokerUsername { get; set; }
        public string MessageBrokerPassword { get; set; }
        public string MessageBrokerHost { get; set; }
    }

    public interface IMessage
    {
        string Exchange { get; set; }
        string Route { get; set; }
        string QName { get; set; }
        ushort Batch { get; set; }
        bool Requeue { get; set; }
    }

    public class Exchange
    {
        public string Name { get; set; }
        public ExchangeTypes ExchangeType { get; set; }
        public bool Durable { get; set; }
        public bool AutoDelete { get; set; }
        public Dictionary<string, object> Arguments { get; set; }
    }

    public enum ExchangeTypes
    {
        direct,
        fanout,
        headers,
        topic
    }

}