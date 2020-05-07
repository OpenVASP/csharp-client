using System.Threading;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient.Sessions
{
    internal class BeneficiarySession : VaspSession
    {
        private readonly IBeneficiaryVaspCallbacks _beneficiaryVaspCallbacks;

        public BeneficiarySession(
            VaspInformation beneficiaryVasp,
            string sessionId,
            string counterpartyTopic,
            string counterPartyPubSigningKey,
            string sharedKey,
            string privateSigningKey,
            IBeneficiaryVaspCallbacks beneficiaryVaspCallbacks,
            ITransportClient transportClient,
            ISignService signService)
            : base(
                beneficiaryVasp,
                counterPartyPubSigningKey, 
                sharedKey, 
                privateSigningKey, 
                transportClient, 
                signService)
        {
            _beneficiaryVaspCallbacks = beneficiaryVaspCallbacks;

            SessionId = sessionId;
            CounterPartyTopic = counterpartyTopic;

            _messageHandlerResolverBuilder
                .AddHandler<TransferRequestMessage>(ProcessTransferRequestMessageAsync)
                .AddHandler<TransferDispatchMessage>(ProcessTransferDispatchMessageAsync)
                .AddHandler<TerminationMessage>(ProcessTerminationMessageAsync);
        }

        public async Task SendTransferReplyMessageAsync(TransferReplyMessage message)
        {
            await _transportClient.SendAsync(new MessageEnvelope
            {
                Topic = CounterPartyTopic,
                SigningKey = _privateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = _sharedSymKeyId
            }, message);
        }

        public async Task SendTransferConfirmationMessageAsync(TransferConfirmationMessage message)
        {
            await _transportClient.SendAsync(new MessageEnvelope
            {
                Topic = CounterPartyTopic,
                SigningKey = _privateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = _sharedSymKeyId
            }, message);
        }

        public async Task StartAsync(SessionReplyMessage.SessionReplyMessageCode code)
        {
            var reply = SessionReplyMessage.Create(
                SessionId,
                code,
                new HandShakeResponse(SessionTopic),
                _vaspInfo);
            CounterParty.VaspInfo = reply.Vasp;
            _sharedSymKeyId = await RegisterSymKeyAsync();

            await _transportClient.SendAsync(new MessageEnvelope
            {
                EncryptionKey = _sharedSymKeyId,
                EncryptionType = EncryptionType.Symmetric,
                Topic = CounterPartyTopic,
                SigningKey = _privateSigningKey
            }, reply);

            StartTopicMonitoring();
        }

        private Task ProcessTransferRequestMessageAsync(TransferRequestMessage message, CancellationToken token)
        {
            return _beneficiaryVaspCallbacks.TransferRequestHandlerAsync(message, this);
        }

        private Task ProcessTransferDispatchMessageAsync(TransferDispatchMessage message, CancellationToken token)
        {
            return _beneficiaryVaspCallbacks.TransferDispatchHandlerAsync(message, this);
        }
    }
}