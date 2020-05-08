using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.Messaging.Messages
{
    public class TransferConfirmationMessage : MessageBase
    {
        public static TransferConfirmationMessage Create(
            Message message,
            Originator originator,
            Beneficiary beneficiary,
            TransferReply transfer,
            Transaction transaction,
            VaspInformation vasp)
        {
            return new TransferConfirmationMessage
            {
                Message = message,
                Originator = originator,
                Beneficiary = beneficiary,
                Transfer = transfer,
                Transaction = transaction,
                Vasp = vasp
            };
        }

        public static TransferConfirmationMessage Create(
            string sessionId,
            TransferConfirmationMessageCode messageCode,
            Originator originator,
            Beneficiary beneficiary,
            TransferReply transfer,
            Transaction transaction,
            VaspInformation vasp)
        {
            return new TransferConfirmationMessage
            {
                Message = new Message(Guid.NewGuid().ToByteArray().ToHex(true), sessionId, GetMessageCode(messageCode), MessageType.TransferConfirmation),
                Originator = originator,
                Beneficiary = beneficiary,
                Transfer = transfer,
                Transaction = transaction,
                Vasp = vasp
            };
        }

        [JsonProperty("originator")]
        public Originator Originator { get; private set; }

        [JsonProperty("beneficiary")]
        public Beneficiary Beneficiary { get; private set; }

        [JsonProperty("transfer")]
        public TransferReply Transfer { get; private set; }

        [JsonProperty("tx")]
        public Transaction Transaction { get; private set; }

        [JsonProperty("vasp")]
        public VaspInformation Vasp { get; private set; }

        public static string GetMessageCode(TransferConfirmationMessageCode messageCode)
        {
            return ((int)messageCode).ToString();
        }
        public enum TransferConfirmationMessageCode
        {
            TransferConfirmed = 1,
            TransferNotConfirmedDispatchNotValid = 2,
            TransferNotConfirmedAssetsNotReceived = 3,
            TransferNotConfirmedWrongAmount = 4,
            TransferNotConfirmedWrongAsset = 5,
            TransferNotConfirmedTransactionDataMissmatch = 6,
        }
    }
}
