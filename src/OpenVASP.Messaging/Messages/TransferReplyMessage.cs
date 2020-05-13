using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.Messaging.Messages
{
    public class TransferReplyMessage : MessageBase
    {
        public static TransferReplyMessage Create(Message message, string destinationAddress)
        {
            return new TransferReplyMessage
            {
                Message = message,
                DestinationAddress = destinationAddress
            };
        }

        public static TransferReplyMessage Create(
            string sessionId,  TransferReplyMessageCode transferReplyMessageCode, string destinationAddress)
        {
            return new TransferReplyMessage
            {
                Message = new Message(Guid.NewGuid().ToByteArray().ToHex(true), sessionId, GetMessageCode(transferReplyMessageCode), MessageType.TransferReply),
                DestinationAddress = destinationAddress
            };
        }
        
        [JsonProperty("destinationAddr")]
        public string DestinationAddress { get; private set; }

        public static string GetMessageCode(TransferReplyMessageCode messageCode)
        {
            return ((int)messageCode).ToString();
        }
        public enum TransferReplyMessageCode
        {
            TransferAccepted = 1,
            TransferDeclinedRequestNotValid = 2,
            TransferDeclinedNoSuchBeneficiary = 3,
            TransferDeclinedVirtualAssetNotSupported = 4,
            TransferDeclinedTransferNotAuthorized = 5,
            TransferDeclinedTemporaryDisruptionOfService = 6,
        }
    }
}
