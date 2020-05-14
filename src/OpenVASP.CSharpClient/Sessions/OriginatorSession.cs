using System;
using System.Threading;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Events;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient.Sessions
{
    public class OriginatorSession : VaspSession
    {
        private readonly VaspCode _vaspCode;
        private readonly IOriginatorVaspCallbacks _originatorVaspCallbacks;

        private MessageEnvelope _messageEnvelope;

        /// <summary>Notifies about received session reply message.</summary>
        public event Func<SessionMessageEvent<SessionReplyMessage>, Task> SessionReplyMessageReceived;
        /// <summary>Notifies about received transfer reply message.</summary>
        public event Func<SessionMessageEvent<TransferReplyMessage>, Task> TransferReplyMessageReceived;
        /// <summary>Notifies about received transfer confirmation message.</summary>
        public event Func<SessionMessageEvent<TransferConfirmationMessage>, Task> TransferConfirmationMessageReceived;

        public OriginatorSessionInfo SessionInfo { get; }

        internal OriginatorSession(
            OriginatorSessionInfo sessionInfo,
            VaspCode vaspCode,
            ITransportClient transportClient,
            ISignService signService,
            IOriginatorVaspCallbacks originatorVaspCallbacks)
            : base(
                sessionInfo,
                transportClient,
                signService)
        {
            _vaspCode = vaspCode;
            _originatorVaspCallbacks = originatorVaspCallbacks;

            SessionInfo = sessionInfo;

            _messageHandlerResolverBuilder
                .AddHandler<SessionReplyMessage>(ProcessSessionReplyMessageAsync)
                .AddHandler<TransferReplyMessage>(ProcessTransferReplyMessageAsync)
                .AddHandler<TransferConfirmationMessage>(ProcessTransferConfirmationMessageAsync);
        }

        public async Task SessionRequestAsync(VaspInformation vaspInfo)
        {
            var sessionRequestMessage = SessionRequestMessage.Create(
                SessionInfo.Id,
                new HandShakeRequest(SessionInfo.Topic, SessionInfo.PublicEncryptionKey),
                vaspInfo);

            await _transportClient.SendAsync(new MessageEnvelope
            {
                Topic = _vaspCode.Code,
                SigningKey = SessionInfo.PrivateSigningKey,
                EncryptionType = EncryptionType.Assymetric,
                EncryptionKey = SessionInfo.PublicHandshakeKey
            }, sessionRequestMessage);
        }

        public async Task TransferRequestAsync(
            Originator originator,
            Beneficiary beneficiary,
            VirtualAssetType type,
            decimal amount)
        {
            var instruction = new TransferInstruction
            {
                VirtualAssetTransfer = new VirtualAssetTransfer
                {
                    TransferType = TransferType.BlockchainTransfer,
                    VirtualAssetType = type,
                    TransferAmount = amount
                }
            };

            var transferRequest = TransferRequestMessage.Create(
                SessionInfo.Id,
                originator,
                beneficiary,
                new TransferRequest(
                    instruction.VirtualAssetTransfer.VirtualAssetType,
                    instruction.VirtualAssetTransfer.TransferType,
                    instruction.VirtualAssetTransfer.TransferAmount));

            await _transportClient.SendAsync(_messageEnvelope, transferRequest);
        }

        public async Task TransferDispatchAsync(string transactionHash, string sendingAddress)
        {
            var transaction = new Transaction(transactionHash, sendingAddress);

            var transferDispatch = TransferDispatchMessage.Create(SessionInfo.Id, transaction);

            await _transportClient.SendAsync(_messageEnvelope, transferDispatch);
        }

        public async Task TerminateAsync(TerminationMessage.TerminationMessageCode terminationMessageCode)
        {
            var terminationMessage = TerminationMessage.Create(
                Id,
                terminationMessageCode);

            await _transportClient.SendAsync(new MessageEnvelope
            {
                Topic = SessionInfo.CounterPartyTopic,
                SigningKey = SessionInfo.PrivateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = SessionInfo.SymKey
            }, terminationMessage);
        }

        private async Task ProcessSessionReplyMessageAsync(SessionReplyMessage message, CancellationToken token)
        {
            SessionInfo.CounterPartyTopic = message.HandShake.TopicB;

            _messageEnvelope = new MessageEnvelope
            {
                Topic = SessionInfo.CounterPartyTopic,
                SigningKey = SessionInfo.PrivateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = SessionInfo.SymKey
            };

            await TriggerAsyncEvent(
                SessionReplyMessageReceived,
                new SessionMessageEvent<SessionReplyMessage>(Id, message));

            await _originatorVaspCallbacks.SessionReplyMessageHandlerAsync(message, this);
        }

        private async Task ProcessTransferReplyMessageAsync(TransferReplyMessage message, CancellationToken token)
        {
            await TriggerAsyncEvent(
                TransferReplyMessageReceived,
                new SessionMessageEvent<TransferReplyMessage>(Id, message));

            await _originatorVaspCallbacks.TransferReplyMessageHandlerAsync(message, this);
        }

        private async Task ProcessTransferConfirmationMessageAsync(TransferConfirmationMessage message, CancellationToken token)
        {
            await TriggerAsyncEvent(
                TransferConfirmationMessageReceived,
                new SessionMessageEvent<TransferConfirmationMessage>(Id, message));

            await _originatorVaspCallbacks.TransferConfirmationMessageHandlerAsync(message, this);
        }
    }
}