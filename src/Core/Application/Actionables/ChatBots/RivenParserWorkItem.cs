using Application.ChatMessages.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Actionables.ChatBots
{
    public class RivenParseTaskWorkItem
    {
        public ChatMessageModel Message { get; set; }
        public List<RivenParseTaskWorkItemDetail> RivenWorkDetails { get; set; } = new List<RivenParseTaskWorkItemDetail>();
    }
}
