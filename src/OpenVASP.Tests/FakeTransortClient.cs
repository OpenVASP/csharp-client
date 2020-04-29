using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenVASP.CSharpClient;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;
using OpenVASP.Tests.Client;

namespace OpenVASP.Tests
{
    public class FakeTransportClient : ITransportClient
    {
        private readonly ConcurrentQueue<TransportMessage> _queue =
            new ConcurrentQueue<TransportMessage>();
        private readonly IMessageFormatter _messageFormatter;
        private readonly ISignService _signService;

        public FakeTransportClient(IMessageFormatter messageFormatter, ISignService signService)
        {
            this._messageFormatter = messageFormatter;
            this._signService = signService;
        }

        public Task<string> RegisterSymKeyAsync(string privateKeyHex)
        {
            return Task.FromResult("TEST");
        }

        public Task<string> RegisterKeyPairAsync(string privateKeyHex)
        {
            return Task.FromResult("TEST");
        }

        public Task<string> CreateMessageFilterAsync(string topicHex, string privateKeyId = null, string symKeyId = null,
            string signingKey = null)
        {
            return Task.FromResult("TEST");
        }

        public Task<string> SendAsync(MessageEnvelope messageEnvelope, MessageBase message)
        {
            var payload = _messageFormatter.GetPayload(message);
            var sign = _signService.SignPayload(payload, messageEnvelope.SigningKey);

            _queue.Enqueue(TransportMessage.CreateMessage(message, payload, sign));

            return Task.FromResult(_queue.Count.ToString());
        }

        public Task<IReadOnlyCollection<TransportMessage>> GetSessionMessagesAsync(string messageFilter)
        {
            var messages = new List<TransportMessage>();
            while (_queue.TryDequeue(out var received))
            {
                messages.Add(received);
            }

            var result = messages.ToArray();

            return Task.FromResult((IReadOnlyCollection<TransportMessage>)result);
        }
    }
}