using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.Messaging.Messages
{
    public class TransferRequestMessage : MessageBase
    {
        public static TransferRequestMessage Create(
            Message message,
            Originator originator,
            Beneficiary beneficiary,
            TransferRequest transfer,
            VaspInformation vasp)
        {
            return new TransferRequestMessage
            {
                Message = message,
                Originator = originator,
                Beneficiary = beneficiary,
                Transfer = transfer,
                Vasp = vasp
            };
        }

        public static TransferRequestMessage Create(
            string sessionId,
            Originator originator,
            Beneficiary beneficiary,
            TransferRequest transfer,
            VaspInformation vasp)
        {
            return new TransferRequestMessage
            {
                Message = new Message(Guid.NewGuid().ToByteArray().ToHex(true), sessionId, "1", MessageType.TransferRequest),
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
        public TransferRequest Transfer { get; private set; }

        [JsonProperty("vasp")]
        public VaspInformation Vasp { get; private set; }
    }
}
