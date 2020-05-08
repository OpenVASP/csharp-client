using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OpenVASP.Messaging.Messages.Entities
{
    public class Message
    {
        public Message(string messageId, string sessionId, string messageCode, MessageType type)
        {
            MessageId = messageId;
            SessionId = sessionId;
            MessageCode = messageCode;
            MessageType = type;
        }
        
        [JsonProperty("type"), JsonConverter(typeof(StringEnumConverter))]
        public MessageType MessageType { get; protected set; }

        [JsonProperty("msgid")]
        public string MessageId { get; private set; }

        [JsonProperty("session")]
        public string SessionId { get; private set; }

        [JsonProperty("code")]
        public string MessageCode { get; private set; }
    }
}