using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;
using OpenVASP.Messaging.MessagingEngine;

namespace OpenVASP.CSharpClient.Sessions
{
    public class OriginatorSession : VaspSession
    {
        private readonly VirtualAssetsAccountNumber _beneficiaryVaan;
        private readonly string _beneficiaryPubHandshakeKey;
        private readonly string _pubEncryptionKey;
        private readonly IOriginatorVaspCallbacks _originatorVaspCallbacks;
        private readonly Originator _originator;

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
            this._beneficiaryVaan = beneficiaryVaan;
            this.SessionId = Guid.NewGuid().ToByteArray().ToHex(true);
            this._beneficiaryPubHandshakeKey = beneficiaryPubHandshakeKey;
            this._pubEncryptionKey = pubEncryptionKey;
            this._originatorVaspCallbacks = originatorVaspCallbacks;
            this._originator = originator;

            _messageHandlerResolverBuilder.AddHandler(typeof(SessionReplyMessage),
                new SessionReplyMessageHandler((sessionReplyMessage, token) =>
                {
                    this.CounterPartyTopic = sessionReplyMessage.HandShake.TopicB;
                    return _originatorVaspCallbacks.SessionReplyMessageHandlerAsync(sessionReplyMessage, this);
                }));

            _messageHandlerResolverBuilder.AddHandler(typeof(TransferReplyMessage),
                new TransferReplyMessageHandler((transferReplyMessage, token) =>
                {
                    return _originatorVaspCallbacks.TransferReplyMessageHandlerAsync(transferReplyMessage, this);
                }));

            _messageHandlerResolverBuilder.AddHandler(typeof(TransferConfirmationMessage),
                new TransferConfirmationMessageHandler((transferDispatchMessage, token) =>
                {
                    return _originatorVaspCallbacks.TransferConfirmationMessageHandlerAsync(transferDispatchMessage,
                        this);
                }));

            _messageHandlerResolverBuilder.AddHandler(typeof(TerminationMessage),
                new TerminationMessageHandler(async (message, token) =>
                {
                    _hasReceivedTerminationMessage = true;

                    await TerminateAsync(message.GetMessageCode());
                }));
        }

        public async Task StartAsync()
        {
            StartTopicMonitoring();

            var sessionRequestMessage = SessionRequestMessage.Create(
                this.SessionId,
                new HandShakeRequest(this._sessionTopic, this._pubEncryptionKey),
                this._vaspInfo);

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
                this.SessionId,
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
                Topic = this.CounterPartyTopic,
                SigningKey = _privateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = _sharedSymKeyId
            }, transferRequest);
        }

        public async Task TransferDispatchAsync(TransferReply transferReply, Transaction transaction, string beneficiaryName)
        {
            var transferRequest = TransferDispatchMessage.Create(
                this.SessionId,
                this._originator,
                new Beneficiary(beneficiaryName, _beneficiaryVaan.Vaan),
                transferReply,
                transaction,
                _vaspInfo
            );

            await _transportClient.SendAsync(new MessageEnvelope()
            {
                Topic = this.CounterPartyTopic,
                SigningKey = _privateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = _sharedSymKeyId
            }, transferRequest);
        }
    }

    public interface IOriginatorVaspCallbacks
    {
        Task SessionReplyMessageHandlerAsync(SessionReplyMessage message, OriginatorSession session);
        Task TransferReplyMessageHandlerAsync(TransferReplyMessage message, OriginatorSession session);
        Task TransferConfirmationMessageHandlerAsync(TransferConfirmationMessage message, OriginatorSession session);
    }

    public class OriginatorVaspCallbacks : IOriginatorVaspCallbacks
    {
        private readonly Func<SessionReplyMessage, OriginatorSession, Task> _sessionReply;
        private readonly Func<TransferReplyMessage, OriginatorSession, Task> _transferReply;
        private readonly Func<TransferConfirmationMessage, OriginatorSession, Task> _transferConfirm;

        public OriginatorVaspCallbacks(
            Func<SessionReplyMessage, OriginatorSession, Task> sessionReply,
            Func<TransferReplyMessage, OriginatorSession, Task> transferReply,
            Func<TransferConfirmationMessage, OriginatorSession, Task> transferConfirm)
        {
            _sessionReply = sessionReply ?? throw new ArgumentNullException(nameof(sessionReply));
            _transferReply = transferReply ?? throw new ArgumentNullException(nameof(transferReply));
            _transferConfirm = transferConfirm ?? throw new ArgumentNullException(nameof(transferConfirm));
        }

        public Task SessionReplyMessageHandlerAsync(SessionReplyMessage message, OriginatorSession session)
        {
            return _sessionReply.Invoke(message, session);
        }

        public Task TransferReplyMessageHandlerAsync(TransferReplyMessage message, OriginatorSession session)
        {
            return _transferReply.Invoke(message, session);
        }

        public Task TransferConfirmationMessageHandlerAsync(TransferConfirmationMessage message,
            OriginatorSession session)
        {
            return _transferConfirm.Invoke(message, session);
        }
    }
}