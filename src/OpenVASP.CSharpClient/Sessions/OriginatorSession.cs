using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Nethereum.Hex.HexConvertors.Extensions;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient.Sessions
{
    internal class OriginatorSession : VaspSession
    {
        private readonly VaspCode _vaspCode;
        private readonly IOriginatorVaspCallbacks _originatorVaspCallbacks;

        private MessageEnvelope _messageEnvelope;

        public OriginatorSession(
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

            _messageHandlerResolverBuilder
                .AddHandler<SessionReplyMessage>(ProcessSessionReplyMessageAsync)
                .AddHandler<TransferReplyMessage>(ProcessTransferReplyMessageAsync)
                .AddHandler<TransferConfirmationMessage>(ProcessTransferConfirmationMessageAsync);
        }

        public async Task SessionRequestAsync(VaspInformation vaspInfo)
        {
            var sessionRequestMessage = SessionRequestMessage.Create(
                Info.Id,
                new HandShakeRequest(Info.Topic, ((OriginatorSessionInfo) Info).PublicEncryptionKey),
                vaspInfo);

            await _transportClient.SendAsync(new MessageEnvelope
            {
                Topic = _vaspCode.Code,
                SigningKey = Info.PrivateSigningKey,
                EncryptionType = EncryptionType.Assymetric,
                EncryptionKey = ((OriginatorSessionInfo) Info).PublicHandshakeKey
            }, sessionRequestMessage);
        }

        public async Task TransferRequestAsync(Originator originator, Beneficiary beneficiary, TransferInstruction instruction)
        {
            var transferRequest = TransferRequestMessage.Create(
                Info.Id,
                originator,
                beneficiary,
                new TransferRequest(
                    instruction.VirtualAssetTransfer.VirtualAssetType,
                    instruction.VirtualAssetTransfer.TransferType,
                    instruction.VirtualAssetTransfer.TransferAmount));

            await _transportClient.SendAsync(_messageEnvelope, transferRequest);
        }

        public async Task TransferDispatchAsync(Transaction transaction)
        {
            var transferDispatch = TransferDispatchMessage.Create(
                Info.Id,
                transaction);

            await _transportClient.SendAsync(_messageEnvelope, transferDispatch);
        }

        public async Task TerminateAsync(TerminationMessage.TerminationMessageCode terminationMessageCode)
        {
            var terminationMessage = TerminationMessage.Create(
                Info.Id,
                terminationMessageCode);

            await _transportClient.SendAsync(new MessageEnvelope
            {
                Topic = Info.CounterPartyTopic,
                SigningKey = Info.PrivateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = Info.SymKey
            }, terminationMessage);
        }

        private Task ProcessSessionReplyMessageAsync(SessionReplyMessage message, CancellationToken token)
        {
            Info.CounterPartyTopic = message.HandShake.TopicB;
            
            _messageEnvelope = new MessageEnvelope
            {
                Topic = Info.CounterPartyTopic,
                SigningKey = Info.PrivateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = Info.SymKey
            };

            return _originatorVaspCallbacks.SessionReplyMessageHandlerAsync(message, this);
        }

        private Task ProcessTransferReplyMessageAsync(TransferReplyMessage message, CancellationToken token)
        {
            return _originatorVaspCallbacks.TransferReplyMessageHandlerAsync(message, this);
        }

        private Task ProcessTransferConfirmationMessageAsync(TransferConfirmationMessage message,
            CancellationToken token)
        {
            return _originatorVaspCallbacks.TransferConfirmationMessageHandlerAsync(message, this);
        }
    }
}