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
        private readonly VirtualAssetsAccountNumber _beneficiaryVaan;
        private readonly string _beneficiaryPubHandshakeKey;
        private readonly string _pubEncryptionKey;
        private readonly Originator _originator;
        private readonly IOriginatorVaspCallbacks _originatorVaspCallbacks;
        private readonly MessagesTimeoutsConfiguration _messagesTimeoutsConfiguration;

        private MessageEnvelope _messageEnvelope;
        private SessionRequestMessage _sentSessionRequestMessage;
        private TransferRequestMessage _sentTransferRequestMessage;
        private TransferDispatchMessage _sentTransferDispatchMessage;

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
                IOriginatorVaspCallbacks originatorVaspCallbacks,
                MessagesTimeoutsConfiguration messagesTimeoutsConfiguration)
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
            _messagesTimeoutsConfiguration = messagesTimeoutsConfiguration;

            _timer.Elapsed += OnTimedEvent;

            SessionId = Guid.NewGuid().ToByteArray().ToHex(true);

            _messageHandlerResolverBuilder
                .AddHandler<SessionReplyMessage>(ProcessSessionReplyMessageAsync)
                .AddHandler<TransferReplyMessage>(ProcessTransferReplyMessageAsync)
                .AddHandler<TransferConfirmationMessage>(ProcessTransferConfirmationMessageAsync)
                .AddHandler<TerminationMessage>(ProcessTerminationMessageAsync);
        }

        public async Task StartAsync(bool initState = true)
        {
            _sharedSymKeyId = await RegisterSymKeyAsync();

            StartTopicMonitoring();

            if (!initState)
                return;

            if (State >= SessionState.SessionRequestSent) // this step was already done
            {
                // TODO log this
                return;
            }
            if (State != SessionState.None) // unexpected state
            {
                // TODO log this
                return;
            }

            var sessionRequestMessage = SessionRequestMessage.Create(
                SessionId,
                new HandShakeRequest(SessionTopic, _pubEncryptionKey),
                _vaspInfo);

            await _transportClient.SendAsync(new MessageEnvelope
            {
                Topic = _beneficiaryVaan.VaspCode.Code,
                SigningKey = _privateSigningKey,
                EncryptionType = EncryptionType.Assymetric,
                EncryptionKey = _beneficiaryPubHandshakeKey
            }, sessionRequestMessage);

            _sentSessionRequestMessage = sessionRequestMessage;

            State = SessionState.SessionRequestSent;

            _retriesCount = 0;
            _timer.Interval = _messagesTimeoutsConfiguration.SessionRequestMessageTimeout.Value.TotalMilliseconds;
            _timer.Enabled = true;
        }

        public async Task TransferRequestAsync(TransferInstruction instruction)
        {
            if (State >= SessionState.TransferRequestSent) // this step was already done
            {
                // TODO log this
                return;
            }
            if (State < SessionState.SessionReplyReceived) // too early for this step
            {
                // TODO log this
                return;
            }

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

            await _transportClient.SendAsync(_messageEnvelope, transferRequest);

            _sentTransferRequestMessage = transferRequest;

            State = SessionState.TransferRequestSent;

            _retriesCount = 0;
            _timer.Interval = _messagesTimeoutsConfiguration.TransferRequestMessageTimeout.Value.TotalMilliseconds;
            _timer.Enabled = true;
        }

        public async Task TransferDispatchAsync(TransferReply transferReply, Transaction transaction, string beneficiaryName)
        {
            if (State >= SessionState.TransferDispatchSent) // this step was already done
            {
                // TODO log this
                return;
            }
            if (State < SessionState.TransferReplyReceived) // too early for this step
            {
                // TODO log this
                return;
            }

            var transferDispatch = TransferDispatchMessage.Create(
                SessionId,
                _originator,
                new Beneficiary(beneficiaryName, _beneficiaryVaan.Vaan),
                transferReply,
                transaction,
                _vaspInfo
            );

            await _transportClient.SendAsync(_messageEnvelope, transferDispatch);

            _sentTransferDispatchMessage = transferDispatch;

            State = SessionState.TransferDispatchSent;

            _retriesCount = 0;
            _timer.Interval = _messagesTimeoutsConfiguration.TransferDispatchMessageTimeout.Value.TotalMilliseconds;
            _timer.Enabled = true;
        }

        private Task ProcessSessionReplyMessageAsync(SessionReplyMessage message, CancellationToken token)
        {
            if (State >= SessionState.SessionReplyReceived) // outdated message
            {
                // TODO log this
                return Task.CompletedTask;
            }
            if (State < SessionState.SessionRequestSent) // unexpected message
            {
                // TODO log this
                return Task.CompletedTask;
            }

            _timer.Enabled = false;
            State = SessionState.SessionReplyReceived;
            CounterPartyTopic = message.HandShake.TopicB;
            _messageEnvelope = new MessageEnvelope
            {
                Topic = CounterPartyTopic,
                SigningKey = _privateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = _sharedSymKeyId
            };

            return _originatorVaspCallbacks.SessionReplyMessageHandlerAsync(message, this);
        }

        private Task ProcessTransferReplyMessageAsync(TransferReplyMessage message, CancellationToken token)
        {
            if (State >= SessionState.TransferReplyReceived) // outdated message
            {
                // TODO log this
                return Task.CompletedTask;
            }
            if (State < SessionState.TransferRequestSent) // unexpected message
            {
                // TODO log this
                return Task.CompletedTask;
            }

            _timer.Enabled = false;
            State = SessionState.TransferReplyReceived;

            return _originatorVaspCallbacks.TransferReplyMessageHandlerAsync(message, this);
        }

        private Task ProcessTransferConfirmationMessageAsync(TransferConfirmationMessage message, CancellationToken token)
        {
            if (State >= SessionState.TransferConfirmationReceived) // outdated message
            {
                // TODO log this
                return Task.CompletedTask;
            }
            if (State < SessionState.TransferDispatchSent) // unexpected message
            {
                // TODO log this
                return Task.CompletedTask;
            }

            _timer.Enabled = false;
            State = SessionState.TransferConfirmationReceived;

            return _originatorVaspCallbacks.TransferConfirmationMessageHandlerAsync(message, this);
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            switch (State)
            {
                case SessionState.SessionRequestSent:
                    if (_retriesCount < _messagesTimeoutsConfiguration.SessionRequestMessageMaxRetriesCount)
                    {
                        _transportClient.SendAsync(new MessageEnvelope
                            {
                                Topic = _beneficiaryVaan.VaspCode.Code,
                                SigningKey = _privateSigningKey,
                                EncryptionType = EncryptionType.Assymetric,
                                EncryptionKey = _beneficiaryPubHandshakeKey
                            }, _sentSessionRequestMessage)
                            .GetAwaiter().GetResult();
                        ++_retriesCount;
                    }
                    else
                    {
                        TerminateAsync(TerminationMessage.TerminationMessageCode.SessionClosedTransferCancelledByOriginator)
                            .GetAwaiter().GetResult();
                    }
                    break;
                case SessionState.TransferRequestSent:
                    if (_retriesCount < _messagesTimeoutsConfiguration.TransferRequestMessageMaxRetriesCount)
                    {
                        _transportClient.SendAsync(_messageEnvelope, _sentTransferRequestMessage)
                            .GetAwaiter().GetResult();
                        ++_retriesCount;
                    }
                    else
                    {
                        TerminateAsync(TerminationMessage.TerminationMessageCode.SessionClosedTransferCancelledByOriginator)
                            .GetAwaiter().GetResult();
                    }
                    break;
                case SessionState.TransferDispatchSent:
                    if (_retriesCount < _messagesTimeoutsConfiguration.TransferDispatchMessageMaxRetriesCount)
                    {
                        _transportClient.SendAsync(_messageEnvelope, _sentTransferDispatchMessage)
                            .GetAwaiter().GetResult();
                        ++_retriesCount;
                    }
                    else
                    {
                        TerminateAsync(TerminationMessage.TerminationMessageCode.SessionClosedTransferCancelledByOriginator)
                            .GetAwaiter().GetResult();
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Timer elapsed in {State} state");
            }
        }
    }
}