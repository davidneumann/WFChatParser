using System;
using System.Collections.Generic;
using System.Text;

namespace Application.ChatMessages.Model
{
    public class ChatMessageModel
    {
        public string Timestamp { get; set; }
        public string Author { get; set; }
        public string EnhancedMessage { get; set; }
        public string Raw { get; set; }
        public List<Riven> Rivens { get; set; } = new List<Riven>();

        public string DEBUGIMAGE { get; set; }
        public string DEBUGREASON { get; set; }
        public DateTimeOffset SystemTimestamp { get; set; }
        public string Region { get; internal set; }
    }
}
