using System.Threading.Tasks;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;
using OpenVASP.Messaging.MessagingEngine;
using OpenVASP.Tests.Client;
using OpenVASP.Tests.Client.Sessions;

namespace OpenVASP.CSharpClient.Sessions
{
    public class BeneficiarySession : VaspSession
    {
        private readonly IVaspMessageHandler _vaspMessageHandler;

        public BeneficiarySession(
            VaspInformation beneficiaryVasp,
            string sessionId,
            string counterpartyTopic,
            string counterPartyPubSigningKey,
            string sharedKey,
            string privateSigningKey,
            IVaspMessageHandler vaspMessageHandler,
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
            this.SessionId = sessionId;
            this.CounterPartyTopic = counterpartyTopic;
            this._vaspMessageHandler = vaspMessageHandler;

            _messageHandlerResolverBuilder.AddHandler(typeof(TransferRequestMessage), new TransferRequestMessageHandler(
                async (message, cancellationToken) =>
                {
                    await _vaspMessageHandler.TransferRequestHandlerAsync(message, this);
                }));

            _messageHandlerResolverBuilder.AddHandler(typeof(TransferDispatchMessage), new TransferDispatchMessageHandler(
                async (message, cancellationToken) =>
                {
                    await _vaspMessageHandler.TransferDispatchHandlerAsync(message, this);
                }));

            _messageHandlerResolverBuilder.AddHandler(typeof(TerminationMessage),
                new TerminationMessageHandler(async (message, token) =>
                {
                    _hasReceivedTerminationMessage = true;
                    await TerminateAsync(message.GetMessageCode());
                }));
        }

        public async Task SendTransferReplyMessageAsync(TransferReplyMessage message)
        {
            await _transportClient.SendAsync(new MessageEnvelope()
            {
                Topic = this.CounterPartyTopic,
                SigningKey = _privateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = _sharedSymKeyId
            }, message);
        }

        public async Task SendTransferConfirmationMessageAsync(TransferConfirmationMessage message)
        {
            await _transportClient.SendAsync(new MessageEnvelope()
            {
                Topic = this.CounterPartyTopic,
                SigningKey = _privateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = _sharedSymKeyId
            }, message);
        }

        public async Task StartAsync(SessionReplyMessage.SessionReplyMessageCode code)
        {
            var reply = SessionReplyMessage.Create(this.SessionId, code, new HandShakeResponse(this.SessionTopic), this._vaspInfo);
            this.CounterParty.VaspInfo = reply.Vasp;
            _sharedSymKeyId = await _transportClient.RegisterSymKeyAsync(_sharedKey);

            await _transportClient.SendAsync(new MessageEnvelope()
            {
                EncryptionKey = _sharedSymKeyId,
                EncryptionType = EncryptionType.Symmetric,
                Topic = this.CounterPartyTopic,
                SigningKey = this._privateSigningKey
            }, reply);

            StartTopicMonitoring();
        }
    }
}