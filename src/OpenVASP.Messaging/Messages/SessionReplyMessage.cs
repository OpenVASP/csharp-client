using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.Messaging.Messages
{
    public class SessionReplyMessage : MessageBase
    {
        public static SessionReplyMessage Create(Message message, HandShakeResponse handshake, VaspInformation vasp)
        {
            return new SessionReplyMessage
            {
                Message = message,
                HandShake = handshake,
                Vasp = vasp
            };
        }

        public static SessionReplyMessage Create(string sessionId, SessionReplyMessageCode code, HandShakeResponse handshake, VaspInformation vasp)
        {
            return new SessionReplyMessage
            {
                Message = new Message(Guid.NewGuid().ToByteArray().ToHex(true), sessionId, GetMessageCode(code), MessageType.SessionReply),
                HandShake = handshake,
                Vasp = vasp
            };
        }

        [JsonProperty("handshake")]
        public HandShakeResponse HandShake { get; private set; }

        [JsonProperty("vasp")]
        public VaspInformation Vasp { get; private set; }

        public static string GetMessageCode(SessionReplyMessageCode messageCode)
        {
            return ((int)messageCode).ToString();
        }

        public enum SessionReplyMessageCode
        {
            SessionAccepted = 1,
            SessionDeclinedRequestNotValid = 2,
            SessionDeclinedOriginatorVaspCouldNotBeAuthenticated = 3,
            SessionDeclinedOriginatorVaspDeclined = 4,
            SessionDeclinedTemporaryDisruptionOfService = 5,
        }
    }
}
