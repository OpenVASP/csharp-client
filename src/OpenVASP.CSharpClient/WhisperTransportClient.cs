using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient
{
    //TODO: add retry policies

    public class WhisperTransportClient : ITransportClient
    {
        private const int SignatureLength = 130;
        private readonly IMessageFormatter _messageFormatter;
        private readonly IWhisperRpc _whisperRpc;
        private readonly ISignService _signService;

        public WhisperTransportClient(IWhisperRpc whisperRpc, ISignService signService, IMessageFormatter messageFormatter)
        {
            _whisperRpc = whisperRpc;
            _signService = signService;
            _messageFormatter = messageFormatter;
        }

        public async Task<string> RegisterSymKeyAsync(string privateKeyHex)
        {
            return await _whisperRpc.RegisterSymKeyAsync(privateKeyHex);
        }

        public async Task<string> RegisterKeyPairAsync(string privateKeyHex)
        {
            return await _whisperRpc.RegisterKeyPairAsync(privateKeyHex);
        }

        public async Task<string> SendAsync(MessageEnvelope messageEnvelope, MessageBase message)
        {
            var payload = _messageFormatter.GetPayload(message);
            var sign = _signService.SignPayload(payload, messageEnvelope.SigningKey);
            
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                return await _whisperRpc.SendMessageAsync(
                    messageEnvelope.Topic,
                    messageEnvelope.EncryptionKey,
                    messageEnvelope.EncryptionType,
                    payload + sign);
            });
        }

        public async Task<string> CreateMessageFilterAsync(string topicHex, string privateKeyId = null,
            string symKeyId = null, string signingKey = null)
        {
            var messageFilter = await _whisperRpc.CreateMessageFilterAsync(
                topicHex,
                privateKeyId,
                symKeyId,
                signingKey);
            return messageFilter;
        }

        public async Task<IReadOnlyCollection<TransportMessage>> GetSessionMessagesAsync(string messageFilter)
        {
            var messages = await RetryPolicy.ExecuteAsync(async () => await _whisperRpc.GetMessagesAsync(messageFilter));

            if (messages == null || messages.Count == 0)
            {
                return new TransportMessage[] { };
            }

            var serializedMessages = messages.Select(x =>
            {
                var payload = x.Payload.Substring(0, x.Payload.Length - SignatureLength);
                var sign = x.Payload.Substring(x.Payload.Length - SignatureLength, SignatureLength);
                var message = _messageFormatter.Deserialize(payload);

                return TransportMessage.CreateMessage(message, payload, sign);
            }).ToArray();

            return serializedMessages;
        }
    }
}