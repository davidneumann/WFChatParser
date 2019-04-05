using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Application.LogParser
{
    public class RedTextMessage
    {
        [JsonProperty("sent")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTimeOffset SentTime { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }
    }
}