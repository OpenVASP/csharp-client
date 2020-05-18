using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenVASP.CSharpClient.Events;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient.Sessions
{
    public class BeneficiarySession : VaspSession
    {
        private readonly IBeneficiaryVaspCallbacks _beneficiaryVaspCallbacks;

        private MessageEnvelope _messageEnvelope;

        /// <summary>Notifies about received session request message.</summary>
        public event Func<SessionMessageEvent<SessionRequestMessage>, Task> SessionRequestMessageReceived;
        /// <summary>Notifies about received transfer request message.</summary>
        public event Func<SessionMessageEvent<TransferRequestMessage>, Task> TransferRequestMessageReceived;
        /// <summary>Notifies about received transfer dispatch message.</summary>
        public event Func<SessionMessageEvent<TransferDispatchMessage>, Task> TransferDispatchMessageReceived;
        /// <summary>Notifies about received termination message.</summary>
        public event Func<SessionMessageEvent<TerminationMessage>, Task> TerminationMessageReceived;

        public BeneficiarySessionInfo SessionInfo { get; }

        internal BeneficiarySession(
            BeneficiarySessionInfo sessionInfo,
            IBeneficiaryVaspCallbacks beneficiaryVaspCallbacks,
            ITransportClient transportClient,
            ISignService signService,
            ILoggerFactory loggerFactory)
            : base(
                sessionInfo,
                transportClient,
                signService,
                loggerFactory)
        {
            _beneficiaryVaspCallbacks = beneficiaryVaspCallbacks;

            SessionInfo = sessionInfo;

            _messageHandlerResolverBuilder
                .AddHandler<TransferRequestMessage>(ProcessTransferRequestMessageAsync)
                .AddHandler<TransferDispatchMessage>(ProcessTransferDispatchMessageAsync)
                .AddHandler<TerminationMessage>(ProcessTerminationMessageAsync);
        }

        public async Task SessionReplyAsync(VaspInformation vaspInfo, SessionReplyMessage.SessionReplyMessageCode? code)
        {
            _messageEnvelope = new MessageEnvelope
            {
                EncryptionKey = SessionInfo.SymKey,
                EncryptionType = EncryptionType.Symmetric,
                Topic = SessionInfo.CounterPartyTopic,
                SigningKey = SessionInfo.PrivateSigningKey
            };

            if (!code.HasValue)
                return;

            var message = SessionReplyMessage.Create(
                Id,
                code.Value,
                new HandShakeResponse(SessionInfo.Topic),
                vaspInfo);

            await _transportClient.SendAsync(_messageEnvelope, message);
        }

        public async Task TransferReplyAsync(TransferReplyMessage message)
        {
            await _transportClient.SendAsync(_messageEnvelope, message);
        }

        public async Task TransferConfirmAsync(TransferConfirmationMessage message)
        {
            await _transportClient.SendAsync(_messageEnvelope, message);
        }

        internal async Task ProcessSessionRequestMessageAsync(SessionRequestMessage message)
        {
            await TriggerAsyncEvent(
                SessionRequestMessageReceived,
                new SessionMessageEvent<SessionRequestMessage>(Id, message));

            await _beneficiaryVaspCallbacks.SessionRequestHandlerAsync(message, this);
        }

        private async Task ProcessTransferRequestMessageAsync(TransferRequestMessage message, CancellationToken token)
        {
            await TriggerAsyncEvent(
                TransferRequestMessageReceived,
                new SessionMessageEvent<TransferRequestMessage>(Id, message));

            await _beneficiaryVaspCallbacks.TransferRequestHandlerAsync(message, this);
        }

        private async Task ProcessTransferDispatchMessageAsync(TransferDispatchMessage message, CancellationToken token)
        {
            await TriggerAsyncEvent(
                TransferDispatchMessageReceived,
                new SessionMessageEvent<TransferDispatchMessage>(Id, message));

            await _beneficiaryVaspCallbacks.TransferDispatchHandlerAsync(message, this);
        }

        private async Task ProcessTerminationMessageAsync(TerminationMessage message, CancellationToken token)
        {
            await TriggerAsyncEvent(
                TerminationMessageReceived,
                new SessionMessageEvent<TerminationMessage>(Id, message));

            await _beneficiaryVaspCallbacks.TerminationHandlerAsync(message, this);
        }
    }
}