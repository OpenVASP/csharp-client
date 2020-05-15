using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.Messaging
{
    public class WhisperMessageFormatter : IMessageFormatter
    {
        private readonly ILogger<WhisperMessageFormatter> _logger;

        public WhisperMessageFormatter(ILogger<WhisperMessageFormatter> logger)
        {
            _logger = logger;
        }

        public string GetPayload(MessageBase messageBase)
        {
            switch (messageBase.Message.MessageType)
            {
                case MessageType.SessionRequest:
                {
                    var str = JsonConvert.SerializeObject((SessionRequestMessage) messageBase);
                    return Encoding.UTF8.GetBytes(str).ToHex(true);
                }

                case MessageType.SessionReply:
                {
                    var str = JsonConvert.SerializeObject((SessionReplyMessage) messageBase);
                    return Encoding.UTF8.GetBytes(str).ToHex(true);
                }

                case MessageType.TransferRequest:
                {
                    var str = JsonConvert.SerializeObject((TransferRequestMessage) messageBase);
                    return Encoding.UTF8.GetBytes(str).ToHex(true);
                }

                case MessageType.TransferReply:
                {
                    var str = JsonConvert.SerializeObject((TransferReplyMessage) messageBase);
                    return Encoding.UTF8.GetBytes(str).ToHex(true);
                }

                case MessageType.TransferDispatch:
                {
                    var str = JsonConvert.SerializeObject((TransferDispatchMessage) messageBase);
                    return Encoding.UTF8.GetBytes(str).ToHex(true);
                }

                case MessageType.TransferConfirmation:
                {
                    var str = JsonConvert.SerializeObject((TransferConfirmationMessage) messageBase);
                    return Encoding.UTF8.GetBytes(str).ToHex(true);
                }

                case MessageType.Termination:
                {
                    var str = JsonConvert.SerializeObject((TerminationMessage) messageBase);
                    return Encoding.UTF8.GetBytes(str).ToHex(true);
                }

                default:
                    throw new ArgumentException(
                        $"Message of type {messageBase.GetType()} contains enum message type {messageBase.Message.MessageType}"
                        + "which is not supported");
            }
        }

        public MessageBase Deserialize(string payload)
        {
            payload = payload.HexToUTF8String();

            var message = JsonConvert.DeserializeObject<MessageBase>(payload);
            switch (message.Message.MessageType)
            {
                case MessageType.SessionRequest:
                    return JsonConvert.DeserializeObject<SessionRequestMessage>(payload);

                case MessageType.SessionReply:
                {
                    return JsonConvert.DeserializeObject<SessionReplyMessage>(payload);
                }

                case MessageType.TransferRequest:
                {
                    return JsonConvert.DeserializeObject<TransferRequestMessage>(payload);
                }

                case MessageType.TransferReply:
                {
                    return JsonConvert.DeserializeObject<TransferReplyMessage>(payload);
                }

                case MessageType.TransferDispatch:
                {
                    return JsonConvert.DeserializeObject<TransferDispatchMessage>(payload);
                }

                case MessageType.TransferConfirmation:
                {
                    return JsonConvert.DeserializeObject<TransferConfirmationMessage>(payload);
                }

                case MessageType.Termination:
                {
                    return JsonConvert.DeserializeObject<TerminationMessage>(payload);
                }

                default:
                    _logger.LogWarning($"Message type {message.Message.MessageType} is not supported");
                    break;
            }

            return message;
        }
    }
}