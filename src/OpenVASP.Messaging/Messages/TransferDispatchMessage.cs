using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.Messaging.Messages
{
    public class TransferDispatchMessage : MessageBase
    {
        public static TransferDispatchMessage Create(
            Message message,
            Transaction transaction)
        {
            return new TransferDispatchMessage
            {
                Message = message,
                Transaction = transaction
            };
        }

        public static TransferDispatchMessage Create(
            string sessionId,
            Transaction transaction)
        {
            return new TransferDispatchMessage
            {
                Message = new Message(Guid.NewGuid().ToByteArray().ToHex(true), sessionId, "1", MessageType.TransferDispatch),
                Transaction = transaction
            };
        }

        [JsonProperty("tx")]
        public Transaction Transaction { get; private set; }
    }
}
