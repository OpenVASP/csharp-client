using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient.Sessions
{
    internal class OriginatorSession : VaspSession
    {
        private readonly VirtualAssetsAccountNumber _beneficiaryVaan;
        private readonly string _beneficiaryPubHandshakeKey;
        private readonly string _pubEncryptionKey;
        private readonly Originator _originator;
        private readonly IOriginatorVaspCallbacks _originatorVaspCallbacks;

        public OriginatorSession(
                Originator originator,
                VaspInformation originatorVasp,
                VirtualAssetsAccountNumber beneficiaryVaan,
                string beneficiaryPubSigningKey,
                string beneficiaryPubHandshakeKey,
                string sharedEncryptionKey,
                string pubEncryptionKey,
                string privateSigningKey,
                ITransportClient transportClient,
                ISignService signService,
                IOriginatorVaspCallbacks originatorVaspCallbacks)
            : base(
                originatorVasp,
                beneficiaryPubSigningKey,
                sharedEncryptionKey,
                privateSigningKey,
                transportClient,
                signService)
        {
            _beneficiaryVaan = beneficiaryVaan;
            _beneficiaryPubHandshakeKey = beneficiaryPubHandshakeKey;
            _pubEncryptionKey = pubEncryptionKey;
            _originator = originator;
            _originatorVaspCallbacks = originatorVaspCallbacks;

            SessionId = Guid.NewGuid().ToByteArray().ToHex(true);

            _messageHandlerResolverBuilder
                .AddHandler<SessionReplyMessage>(ProcessSessionReplyMessageAsync)
                .AddHandler<TransferReplyMessage>(ProcessTransferReplyMessageAsync)
                .AddHandler<TransferConfirmationMessage>(ProcessTransferConfirmationMessageAsync)
                .AddHandler<TerminationMessage>(ProcessTerminationMessageAsync);
        }

        public async Task StartAsync()
        {
            StartTopicMonitoring();

            var sessionRequestMessage = SessionRequestMessage.Create(
                SessionId,
                new HandShakeRequest(SessionTopic, _pubEncryptionKey),
                _vaspInfo);

            await _transportClient.SendAsync(new MessageEnvelope()
            {
                Topic = _beneficiaryVaan.VaspCode.Code,
                SigningKey = _privateSigningKey,
                EncryptionType = EncryptionType.Assymetric,
                EncryptionKey = _beneficiaryPubHandshakeKey
            }, sessionRequestMessage);
        }

        public async Task TransferRequestAsync(TransferInstruction instruction)
        {
            var transferRequest = TransferRequestMessage.Create(
                SessionId,
                _originator,
                new Beneficiary(instruction.BeneficiaryName ?? string.Empty, _beneficiaryVaan.Vaan),
                new TransferRequest(
                    instruction.VirtualAssetTransfer.VirtualAssetType,
                    instruction.VirtualAssetTransfer.TransferType,
                    instruction.VirtualAssetTransfer.TransferAmount),
                _vaspInfo
            );

            await _transportClient.SendAsync(new MessageEnvelope()
            {
                Topic = CounterPartyTopic,
                SigningKey = _privateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = _sharedSymKeyId
            }, transferRequest);
        }

        public async Task TransferDispatchAsync(TransferReply transferReply, Transaction transaction, string beneficiaryName)
        {
            var transferRequest = TransferDispatchMessage.Create(
                SessionId,
                _originator,
                new Beneficiary(beneficiaryName, _beneficiaryVaan.Vaan),
                transferReply,
                transaction,
                _vaspInfo
            );

            await _transportClient.SendAsync(new MessageEnvelope()
            {
                Topic = CounterPartyTopic,
                SigningKey = _privateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = _sharedSymKeyId
            }, transferRequest);
        }

        private Task ProcessSessionReplyMessageAsync(SessionReplyMessage message, CancellationToken token)
        {
            CounterPartyTopic = message.HandShake.TopicB;
            return _originatorVaspCallbacks.SessionReplyMessageHandlerAsync(message, this);
        }

        private Task ProcessTransferReplyMessageAsync(TransferReplyMessage message, CancellationToken token)
        {
            return _originatorVaspCallbacks.TransferReplyMessageHandlerAsync(message, this);
        }

        private Task ProcessTransferConfirmationMessageAsync(TransferConfirmationMessage message, CancellationToken token)
        {
            return _originatorVaspCallbacks.TransferConfirmationMessageHandlerAsync(message, this);
        }
    }
}