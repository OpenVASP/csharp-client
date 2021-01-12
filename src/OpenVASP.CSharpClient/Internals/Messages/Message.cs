using Newtonsoft.Json;

namespace OpenVASP.CSharpClient.Internals.Messages
{
    public class Message
    {
        [JsonProperty("content")]
        public MessageContent Content { get; set; }

        [JsonProperty("sig")]
        public string Signature { get; set; }

        public Message()
        {
        }

        public Message(MessageContent messageContent)
        {
            Content = messageContent;
        }
    }
}