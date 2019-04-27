using Application.ChatMessages.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Application.Actionables.ChatBots
{
    public class RivenParseTaskWorkItem
    {
        public ChatMessageModel Message { get; set; }
        public List<RivenParseTaskWorkItemDetail> RivenWorkDetails { get; set; } = new List<RivenParseTaskWorkItemDetail>();
        public ConcurrentQueue<string> MessageCache { get; set; }
        public ConcurrentDictionary<string, ChatMessageModel> MessageCacheDetails { get; set}
    }
}
