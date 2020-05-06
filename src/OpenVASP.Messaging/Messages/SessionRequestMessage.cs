using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.Messaging.Messages
{
    public class SessionRequestMessage : MessageBase
    {
        [JsonProperty("handshake")]
        public HandShakeRequest HandShake { get; private set; }

        [JsonProperty("vasp")]
        public VaspInformation Vasp { get; private set; }

        public static SessionRequestMessage Create(Message message, HandShakeRequest handshake, VaspInformation vasp)
        {
            return new SessionRequestMessage
            {
                Message = message,
                HandShake = handshake,
                Vasp = vasp
            };
        }

        public static SessionRequestMessage Create(string sessionId, HandShakeRequest handshake, VaspInformation vasp)
        {
            return new SessionRequestMessage
            {
                Message = new Message(Guid.NewGuid().ToByteArray().ToHex(true), sessionId, "1", MessageType.SessionRequest),
                HandShake = handshake,
                Vasp = vasp
            };
        }
    }
}
