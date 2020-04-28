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
            Originator originator,
            Beneficiary beneficiary,
            TransferReply transfer,
            Transaction transaction,
            VaspInformation vasp)
        {
            return new TransferDispatchMessage
            {
                Message = message,
                Originator = originator,
                Beneficiary = beneficiary,
                Transfer = transfer,
                Transaction = transaction,
                Vasp = vasp
            };
        }

        public static TransferDispatchMessage Create(
            string sessionId,
            Originator originator,
            Beneficiary beneficiary,
            TransferReply transfer,
            Transaction transaction,
            VaspInformation vasp)
        {
            return new TransferDispatchMessage
            {
                Message = new Message(Guid.NewGuid().ToByteArray().ToHex(true), sessionId, "1", MessageType.TransferDispatch),
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
    }
}
