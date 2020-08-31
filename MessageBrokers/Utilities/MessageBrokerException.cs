using System;

namespace MessageBrokers.Utilities
{
    public class MessageBrokerException : Exception
    {
        public MessageBrokerException(string message)
            : base(message)
        {
        }
    }
}
