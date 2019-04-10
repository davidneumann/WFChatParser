using System;
using System.Runtime.Serialization;

namespace Application
{
    [Serializable]
    internal class ChatMissingException : Exception
    {
        public ChatMissingException()
        {
        }

        public ChatMissingException(string message) : base(message)
        {
        }

        public ChatMissingException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ChatMissingException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}