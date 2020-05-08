using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.Messaging.Messages
{
    public class TransferReplyMessage : MessageBase
    {
        public static TransferReplyMessage Create(
            Message message,
            Originator originator,
            Beneficiary beneficiary,
            TransferReply transfer,
            VaspInformation vasp)
        {
            return new TransferReplyMessage
            {
                Message = message,
                Originator = originator,
                Beneficiary = beneficiary,
                Transfer = transfer,
                Vasp = vasp
            };
        }

        public static TransferReplyMessage Create(
            string sessionId,
            TransferReplyMessageCode transferReplyMessageCode,
            Originator originator,
            Beneficiary beneficiary,
            TransferReply transfer,
            VaspInformation vasp)
        {
            return new TransferReplyMessage
            {
                Message = new Message(Guid.NewGuid().ToByteArray().ToHex(true), sessionId, GetMessageCode(transferReplyMessageCode), MessageType.TransferReply),
                Originator = originator,
                Beneficiary = beneficiary,
                Transfer = transfer,
                Vasp = vasp
            };
        }

        [JsonProperty("originator")]
        public Originator Originator { get; private set; }

        [JsonProperty("beneficiary")]
        public Beneficiary Beneficiary { get; private set; }

        [JsonProperty("transfer")]
        public TransferReply Transfer { get; private set; }

        [JsonProperty("vasp")]
        public VaspInformation Vasp { get; private set; }

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
