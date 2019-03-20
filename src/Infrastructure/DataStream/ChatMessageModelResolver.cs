using Application.ChatMessages.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataStream
{
    internal class ChatMessageModelResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            IList<JsonProperty> props = base.CreateProperties(type, memberSerialization);
            return props.Where(p => p.PropertyName != nameof(ChatMessageModel.Timestamp)).ToList();
        }

        //protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        //{
        //    IList<JsonProperty> props = base.CreateProperties(type, memberSerialization);
        //    return props.Where(p => p.Writable).ToList();
        //}
    }
}
