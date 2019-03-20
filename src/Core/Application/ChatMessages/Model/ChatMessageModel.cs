using System;
using System.Collections.Generic;
using System.Text;

namespace Application.ChatMessages.Model
{
    public class ChatMessageModel
    {
        public string Timestamp { get; set; }
        public string Author { get; set; }
        public string SpecialMessage { get; set; }
        public string Raw { get; set; }
        public Riven[] Rivens { get; set; }

        public string DEBUGIMAGE { get; set; }
        public string DEBUGREASON { get; set; }
        public double SystemTimestamp { get; set; }
    }
}
