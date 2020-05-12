using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient.Sessions
{
    internal class BeneficiarySession : VaspSession
    {
        private readonly IBeneficiaryVaspCallbacks _beneficiaryVaspCallbacks;

        private MessageEnvelope _messageEnvelope;

        public BeneficiarySession(
            BeneficiarySessionInfo sessionInfo,
            IBeneficiaryVaspCallbacks beneficiaryVaspCallbacks,
            ITransportClient transportClient,
            ISignService signService)
            : base(
                sessionInfo,
                transportClient,
                signService)
        {
            _beneficiaryVaspCallbacks = beneficiaryVaspCallbacks;

            _messageHandlerResolverBuilder
                .AddHandler<TransferRequestMessage>(ProcessTransferRequestMessageAsync)
                .AddHandler<TransferDispatchMessage>(ProcessTransferDispatchMessageAsync)
                .AddHandler<TerminationMessage>(ProcessTerminationMessageAsync);
        }

        public async Task SessionReplyAsync(VaspInformation vaspInfo, SessionReplyMessage.SessionReplyMessageCode? code)
        {
            _messageEnvelope = new MessageEnvelope
            {
                EncryptionKey = Info.SymKey,
                EncryptionType = EncryptionType.Symmetric,
                Topic = Info.CounterPartyTopic,
                SigningKey = Info.PrivateSigningKey
            };

            if (!code.HasValue)
                return;

            var message = SessionReplyMessage.Create(
                Info.Id,
                code.Value,
                new HandShakeResponse(Info.Topic),
                vaspInfo);

            await _transportClient.SendAsync(_messageEnvelope, message);
        }

        public async Task SendTransferReplyMessageAsync(TransferReplyMessage message)
        {
            await _transportClient.SendAsync(_messageEnvelope, message);
        }

        public async Task SendTransferConfirmationMessageAsync(TransferConfirmationMessage message)
        {
            await _transportClient.SendAsync(_messageEnvelope, message);
        }

        private Task ProcessTransferRequestMessageAsync(TransferRequestMessage message, CancellationToken token)
        {
            return _beneficiaryVaspCallbacks.TransferRequestHandlerAsync(message, this);
        }

        private Task ProcessTransferDispatchMessageAsync(TransferDispatchMessage message, CancellationToken token)
        {
            return _beneficiaryVaspCallbacks.TransferDispatchHandlerAsync(message, this);
        }

        private Task ProcessTerminationMessageAsync(TerminationMessage message, CancellationToken token)
        {
            return _beneficiaryVaspCallbacks.TerminationHandlerAsync(message, this);
        }
    }
}