using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Application.LogParser
{
    public class RedTextMessage
    {
        public DateTimeOffset SentTime { get; set; }
        public string Name { get; set; }
        public string Message { get; set; }
    }
}