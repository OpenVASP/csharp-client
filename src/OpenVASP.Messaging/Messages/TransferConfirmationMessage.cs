using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.Messaging.Messages
{
    public class TransferConfirmationMessage : MessageBase
    {
        public static TransferConfirmationMessage Create(Message message)
        {
            return new TransferConfirmationMessage
            {
                Message = message
            };
        }

        public static TransferConfirmationMessage Create(string sessionId, TransferConfirmationMessageCode messageCode)
        {
            return new TransferConfirmationMessage
            {
                Message = new Message(Guid.NewGuid().ToByteArray().ToHex(true), sessionId, GetMessageCode(messageCode), MessageType.TransferConfirmation)
            };
        }

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
