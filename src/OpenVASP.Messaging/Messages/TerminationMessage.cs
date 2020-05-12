using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.Messaging.Messages
{
    public class TerminationMessage : MessageBase
    {
        public static TerminationMessage Create(Message message)
        {
            return new TerminationMessage
            {
                Message = message
            };
        }

        public static TerminationMessage Create(string sessionId, TerminationMessageCode messageCode)
        {
            return new TerminationMessage
            {
                Message = new Message(
                Guid.NewGuid().ToByteArray().ToHex(true),
                    sessionId,
                    GetMessageCode(messageCode),
                    MessageType.Termination)
            };
        }

        public TerminationMessageCode GetMessageCode()
        {
            Enum.TryParse<TerminationMessageCode>(this.Message.MessageCode, out var result);

            return result;
        }

        public static string GetMessageCode(TerminationMessageCode messageCode)
        {
            return ((int)messageCode).ToString();
        }

        public enum TerminationMessageCode
        {
            SessionClosedTransferOccured = 1,
            SessionClosedTransferDeclinedByBeneficiaryVasp = 2,
            SessionClosedTransferCancelledByOriginator = 3,
        }
    }
}
