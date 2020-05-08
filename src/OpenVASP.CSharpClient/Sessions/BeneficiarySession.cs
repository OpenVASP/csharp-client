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
        private readonly MessagesTimeoutsConfiguration _messagesTimeoutsConfiguration;

        private MessageEnvelope _messageEnvelope;
        private SessionReplyMessage _sentSessionReplyMessage;
        private TransferReplyMessage _sentTransferReplyMessage;
        private TransferConfirmationMessage _sentTransferConfirmationMessage;

        public BeneficiarySession(
            VaspInformation beneficiaryVasp,
            string sessionId,
            string counterpartyTopic,
            string counterPartyPubSigningKey,
            string sharedKey,
            string privateSigningKey,
            IBeneficiaryVaspCallbacks beneficiaryVaspCallbacks,
            ITransportClient transportClient,
            ISignService signService,
            MessagesTimeoutsConfiguration messagesTimeoutsConfiguration)
            : base(
                beneficiaryVasp,
                counterPartyPubSigningKey,
                sharedKey,
                privateSigningKey,
                transportClient,
                signService)
        {
            _beneficiaryVaspCallbacks = beneficiaryVaspCallbacks;
            _messagesTimeoutsConfiguration = messagesTimeoutsConfiguration;

            State = SessionState.SessionRequestReceived;

            _timer.Elapsed += OnTimedEvent;

            SessionId = sessionId;
            CounterPartyTopic = counterpartyTopic;
            CounterParty.VaspInfo = _vaspInfo;

            _messageHandlerResolverBuilder
                .AddHandler<TransferRequestMessage>(ProcessTransferRequestMessageAsync)
                .AddHandler<TransferDispatchMessage>(ProcessTransferDispatchMessageAsync)
                .AddHandler<TerminationMessage>(ProcessTerminationMessageAsync);
        }

        public async Task StartAsync(SessionReplyMessage.SessionReplyMessageCode? code)
        {
            _sharedSymKeyId = await RegisterSymKeyAsync();

            StartTopicMonitoring();

            _messageEnvelope = new MessageEnvelope
            {
                EncryptionKey = _sharedSymKeyId,
                EncryptionType = EncryptionType.Symmetric,
                Topic = CounterPartyTopic,
                SigningKey = _privateSigningKey
            };

            if (!code.HasValue)
                return;

            if (State >= SessionState.SessionReplySent) // this step was already done
            {
                // TODO log this
                return;
            }
            if (State < SessionState.SessionRequestReceived) // too early for this step
            {
                // TODO log this
                return;
            }

            var message = SessionReplyMessage.Create(
                SessionId,
                code.Value,
                new HandShakeResponse(SessionTopic),
                _vaspInfo);

            await _transportClient.SendAsync(_messageEnvelope, message);

            _sentSessionReplyMessage = message;

            State = SessionState.SessionReplySent;

            _retriesCount = 0;
            _timer.Interval = _messagesTimeoutsConfiguration.SessionReplyMessageTimeout.Value.TotalMilliseconds;
            _timer.Enabled = true;
        }

        public async Task SendTransferReplyMessageAsync(TransferReplyMessage message)
        {
            if (State >= SessionState.TransferReplySent) // this step was already done
            {
                // TODO log this
                return;
            }
            if (State < SessionState.TransferRequestReceived) // too early for this step
            {
                // TODO log this
                return;
            }

            await _transportClient.SendAsync(_messageEnvelope, message);

            _sentTransferReplyMessage = message;

            State = SessionState.TransferReplySent;

            _retriesCount = 0;
            _timer.Interval = _messagesTimeoutsConfiguration.TransferReplyMessageTimeout.Value.TotalMilliseconds;
            _timer.Enabled = true;
        }

        public async Task SendTransferConfirmationMessageAsync(TransferConfirmationMessage message)
        {
            if (State >= SessionState.TransferConfirmationSent) // this step was already done
            {
                // TODO log this
                return;
            }
            if (State < SessionState.TransferDispatchReceived) // too early for this step
            {
                // TODO log this
                return;
            }

            await _transportClient.SendAsync(_messageEnvelope, message);

            _sentTransferConfirmationMessage = message;

            State = SessionState.TransferConfirmationSent;

            _retriesCount = 0;
            _timer.Interval = _messagesTimeoutsConfiguration.TransferConfirmationMessageTimeout.Value.TotalMilliseconds;
            _timer.Enabled = true;
        }

        private Task ProcessTransferRequestMessageAsync(TransferRequestMessage message, CancellationToken token)
        {
            if (State >= SessionState.TransferRequestReceived) // outdated message
            {
                // TODO log this
                return Task.CompletedTask;
            }
            if (State < SessionState.SessionReplySent) // unexpected message
            {
                // TODO log this
                return Task.CompletedTask;
            }

            _timer.Enabled = false;
            State = SessionState.TransferRequestReceived;

            return _beneficiaryVaspCallbacks.TransferRequestHandlerAsync(message, this);
        }

        private Task ProcessTransferDispatchMessageAsync(TransferDispatchMessage message, CancellationToken token)
        {
            if (State >= SessionState.TransferDispatchReceived) // outdated message
            {
                // TODO log this
                return Task.CompletedTask;
            }
            if (State < SessionState.TransferReplySent) // unexpected message
            {
                // TODO log this
                return Task.CompletedTask;
            }

            _timer.Enabled = false;
            State = SessionState.TransferDispatchReceived;

            return _beneficiaryVaspCallbacks.TransferDispatchHandlerAsync(message, this);
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            switch (State)
            {
                case SessionState.SessionReplySent:
                    if (_retriesCount < _messagesTimeoutsConfiguration.SessionReplyMessageMaxRetriesCount)
                    {
                        _transportClient.SendAsync(_messageEnvelope, _sentSessionReplyMessage)
                            .GetAwaiter().GetResult();
                        ++_retriesCount;
                    }
                    else
                    {
                        TerminateAsync(TerminationMessage.TerminationMessageCode.SessionClosedTransferDeclinedByBeneficiaryVasp)
                            .GetAwaiter().GetResult();
                    }
                    break;
                case SessionState.TransferReplySent:
                    if (_retriesCount < _messagesTimeoutsConfiguration.TransferReplyMessageMaxRetriesCount)
                    {
                        _transportClient.SendAsync(_messageEnvelope, _sentTransferReplyMessage)
                            .GetAwaiter().GetResult();
                        ++_retriesCount;
                    }
                    else
                    {
                        TerminateAsync(TerminationMessage.TerminationMessageCode.SessionClosedTransferDeclinedByBeneficiaryVasp)
                            .GetAwaiter().GetResult();
                    }
                    break;
                case SessionState.TransferConfirmationSent:
                    if (_retriesCount < _messagesTimeoutsConfiguration.TransferConfirmationMessageMaxRetriesCount)
                    {
                        _transportClient.SendAsync(_messageEnvelope, _sentTransferConfirmationMessage)
                            .GetAwaiter().GetResult();
                        ++_retriesCount;
                    }
                    else
                    {
                        TerminateAsync(TerminationMessage.TerminationMessageCode.SessionClosedTransferDeclinedByBeneficiaryVasp)
                            .GetAwaiter().GetResult();
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Timer elapsed in {State} state");
            }
        }
    }
}