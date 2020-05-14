using System;
using Newtonsoft.Json;

namespace OpenVASP.Messaging.Messages.Entities
{
    public class Transaction
    {
        public Transaction(string transactionId, string sendingAddress)
        {
            TransactionId = transactionId;
            DateTime = DateTime.UtcNow;
            SendingAddress = sendingAddress;
        }

        [JsonProperty("txid")]
        public string TransactionId { get; private set; }

        [JsonProperty("datetime")]
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-ddThh:mm:ssZ")]
        public DateTime DateTime { get; private set; }

        [JsonProperty("sendingadr")]
        public string SendingAddress { get; private set; }
    }
}