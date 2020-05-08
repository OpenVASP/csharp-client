using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Cryptography;
using OpenVASP.CSharpClient.Events;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient
{
    /// <summary>
    /// Vasp client is a main class in OpenVasp protocol.
    /// It start listening to incoming Session Requests as beneficiary Vasp.
    /// It can request a session from beneficiary Vasp as originator.
    /// </summary>
    public class VaspClient : IDisposable
    {
        private readonly IEthereumRpc _ethereumRpc;
        private readonly IEnsProvider _ensProvider;
        private readonly ITransportClient _transportClient;
        private readonly ISignService _signService;
        private readonly SessionsRequestsListener _sessionsRequestsListener;
        private readonly MessagesTimeoutsConfiguration _messagesTimeoutsConfiguration;
        private readonly IOriginatorVaspCallbacks _originatorVaspCallbacks;
        private readonly ConcurrentDictionary<string, BeneficiarySession> _beneficiarySessionsDict = 
            new ConcurrentDictionary<string, BeneficiarySession>();
        private readonly ConcurrentDictionary<string, OriginatorSession> _originatorSessionsDict = 
            new ConcurrentDictionary<string, OriginatorSession>();
        private readonly string _signatureKey;

        /// <summary>Notifies about session termination.</summary>
        public event Func<SessionTerminationEvent, Task> SessionTerminated;
        /// <summary>Notifies about session creation.</summary>
        public event Func<SessionCreatedEvent, Task> SessionCreated;
        /// <summary>Notifies about received session request message.</summary>
        public event Func<SessionMessageEvent<SessionRequestMessage>, Task> SessionRequestMessageReceived;
        /// <summary>Notifies about received session reply message.</summary>
        public event Func<SessionMessageEvent<SessionReplyMessage>, Task> SessionReplyMessageReceived;
        /// <summary>Notifies about received transfer reply message.</summary>
        public event Func<SessionMessageEvent<TransferReplyMessage>, Task> TransferReplyMessageReceived;
        /// <summary>Notifies about received transfer confirmation message.</summary>
        public event Func<SessionMessageEvent<TransferConfirmationMessage>, Task> TransferConfirmationMessageReceived;
        /// <summary>Notifies about received transfer request message.</summary>
        public event Func<SessionMessageEvent<TransferRequestMessage>, Task> TransferRequestMessageReceived;
        /// <summary>Notifies about received transfer dispatch message.</summary>
        public event Func<SessionMessageEvent<TransferDispatchMessage>, Task> TransferDispatchMessageReceived;

        /// <summary>
        /// VASP Code
        /// </summary>
        public VaspCode VaspCode { get; }

        /// <summary>
        /// VASP Information
        /// </summary>
        public VaspInformation VaspInfo { get; }

        private VaspClient(
            ECDH_Key handshakeKey,
            string signatureHexKey,
            VaspCode vaspCode,
            VaspInformation vaspInfo,
            IEthereumRpc nodeClientEthereumRpc,
            IEnsProvider ensProvider,
            ITransportClient transportClient,
            ISignService signService,
            MessagesTimeoutsConfiguration messagesTimeoutsConfiguration)
        {
            _signatureKey = signatureHexKey;
            VaspCode = vaspCode;
            VaspInfo = vaspInfo;
            _ethereumRpc = nodeClientEthereumRpc;
            _ensProvider = ensProvider;
            _transportClient = transportClient;
            _signService = signService;
            _messagesTimeoutsConfiguration = messagesTimeoutsConfiguration;

            _originatorVaspCallbacks = new OriginatorVaspCallbacks(
                async (message, session) =>
                {
                    await TriggerAsyncEvent(
                        SessionReplyMessageReceived,
                        new SessionMessageEvent<SessionReplyMessage>(session.SessionId, message));
                },
                async (message, session) =>
                {
                    await TriggerAsyncEvent(
                        TransferReplyMessageReceived,
                        new SessionMessageEvent<TransferReplyMessage>(session.SessionId, message));
                    if (message.Message.MessageCode != "1") //todo: handle properly.
                    {
                        await session.TerminateAsync(TerminationMessage.TerminationMessageCode.SessionClosedTransferOccured);
                        await session.WaitAsync();
                    }
                },
                async (message, session) =>
                {
                    await TriggerAsyncEvent(
                        TransferConfirmationMessageReceived,
                        new SessionMessageEvent<TransferConfirmationMessage>(session.SessionId, message));
                    await session.TerminateAsync(TerminationMessage.TerminationMessageCode.SessionClosedTransferOccured);
                    await session.WaitAsync();
                });

            IBeneficiaryVaspCallbacks beneficiaryVaspCallbacks = new BeneficiaryVaspCallbacks(
                async (request, currentSession) =>
                {
                    _beneficiarySessionsDict[currentSession.SessionId] = currentSession as BeneficiarySession;
                    await TriggerAsyncEvent(
                        SessionRequestMessageReceived,
                        new SessionMessageEvent<SessionRequestMessage>(currentSession.SessionId, request));
                },
                async (request, currentSession) =>
                {
                    await TriggerAsyncEvent(
                        TransferRequestMessageReceived,
                        new SessionMessageEvent<TransferRequestMessage>(currentSession.SessionId, request));
                },
                async (dispatch, currentSession) =>
                {
                    await TriggerAsyncEvent(
                        TransferDispatchMessageReceived,
                        new SessionMessageEvent<TransferDispatchMessage>(currentSession.SessionId, dispatch));
                });

            _sessionsRequestsListener = new SessionsRequestsListener(
                handshakeKey,
                signatureHexKey,
                vaspCode,
                vaspInfo,
                nodeClientEthereumRpc,
                transportClient,
                signService,
                _messagesTimeoutsConfiguration);
            _sessionsRequestsListener.SessionCreated += BeneficiarySessionCreatedAsync;
            _sessionsRequestsListener.StartTopicMonitoring(beneficiaryVaspCallbacks);
        }

        private Task BeneficiarySessionCreatedAsync(BeneficiarySession session)
        {
            _beneficiarySessionsDict.TryAdd(session.SessionId, session);
            session.OnSessionTermination += ProcessSessionTerminationAsync;
            return NotifySessionCreatedAsync(session);
        }

        public async Task<string> CreateSessionAsync(Originator originator, VirtualAssetsAccountNumber beneficiaryVaan)
        {
            string counterPartyVaspContractAddress =
                await _ensProvider.GetContractAddressByVaspCodeAsync(beneficiaryVaan.VaspCode);
            var contractInfo = await _ethereumRpc.GetVaspContractInfoAync(counterPartyVaspContractAddress);
            var sessionKey = ECDH_Key.GenerateKey();
            var sharedKey = sessionKey.GenerateSharedSecretHex(contractInfo.HandshakeKey);

            var session = new OriginatorSession(
                originator,
                VaspInfo,
                beneficiaryVaan,
                contractInfo.SigningKey,
                contractInfo.HandshakeKey,
                sharedKey,
                sessionKey.PublicKey,
                _signatureKey,
                _transportClient,
                _signService,
                _originatorVaspCallbacks,
                _messagesTimeoutsConfiguration);

            await session.StartAsync();

            _originatorSessionsDict.TryAdd(session.SessionId, session);
            session.OnSessionTermination += ProcessSessionTerminationAsync;
            await NotifySessionCreatedAsync(session);

            return session.SessionId;
        }

        public async Task SessionReplyAsync(string sessionId, SessionReplyMessage.SessionReplyMessageCode code)
        {
            if (!_beneficiarySessionsDict.TryGetValue(sessionId, out var session))
                throw new ArgumentException($"Beneficiary session with id {sessionId} not found");

            await session.StartAsync(code);
        }

        public async Task TransferRequestAsync(
            string sessionId,
            string beneficiaryName,
            VirtualAssetType type,
            decimal amount)
        {
            if (!_originatorSessionsDict.TryGetValue(sessionId, out var session))
                throw new ArgumentException($"Originator session with id {sessionId} not found");

            await session.TransferRequestAsync(
                new TransferInstruction
                {
                    VirtualAssetTransfer = new VirtualAssetTransfer
                    {
                        TransferType = TransferType.BlockchainTransfer,
                        VirtualAssetType = type,
                        TransferAmount = amount
                    },
                    BeneficiaryName = beneficiaryName
                });
        }

        public async Task TransferReplyAsync(string sessionId, TransferReplyMessage message)
        {
            if (!_beneficiarySessionsDict.TryGetValue(sessionId, out var session))
                throw new ArgumentException($"Beneficiary session with id {sessionId} not found");

            await session.SendTransferReplyMessageAsync(message);
        }

        public async Task TransferDispatchAsync(
            string sessionId,
            TransferReply transferReply,
            string transactionHash,
            string sendingAddress,
            string beneficiaryName)
        {
            if (!_originatorSessionsDict.TryGetValue(sessionId, out var session))
                throw new ArgumentException($"Originator session with id {sessionId} not found");

            await session.TransferDispatchAsync(
                transferReply,
                new Transaction(
                    transactionHash,
                    DateTime.UtcNow,
                    sendingAddress),
                beneficiaryName);
        }

        public async Task TransferConfirmAsync(string sessionId, TransferConfirmationMessage message)
        {
            if (!_beneficiarySessionsDict.TryGetValue(sessionId, out var session))
                throw new ArgumentException($"Beneficiary session with id {sessionId} not found");

            await session.SendTransferConfirmationMessageAsync(message);
        }

        public static VaspClient Create(
            VaspInformation vaspInfo,
            VaspCode vaspCode,
            string handshakePrivateKeyHex,
            string signaturePrivateKeyHex,
            IEthereumRpc nodeClientEthereumRpc,
            IEnsProvider ensProvider,
            ISignService signService,
            ITransportClient transportClient,
            MessagesTimeoutsConfiguration messagesTimeoutsConfiguration = null)
        {
            var handshakeKey = ECDH_Key.ImportKey(handshakePrivateKeyHex);

            var vaspClient = new VaspClient(
                handshakeKey,
                signaturePrivateKeyHex,
                vaspCode,
                vaspInfo,
                nodeClientEthereumRpc,
                ensProvider,
                transportClient,
                signService,
                messagesTimeoutsConfiguration ?? new MessagesTimeoutsConfiguration());

            return vaspClient;
        }

        public void Dispose()
        {
            _sessionsRequestsListener.Stop();

            var tasks = new List<Task>();
            foreach (var beneficiarySession in _beneficiarySessionsDict.Values)
            {
                beneficiarySession.OnSessionTermination -= ProcessSessionTerminationAsync;
                tasks.Add(
                    beneficiarySession.TerminateAsync(TerminationMessage.TerminationMessageCode.SessionClosedTransferDeclinedByBeneficiaryVasp)
                        .ContinueWith(async _ => await beneficiarySession.WaitAsync()));
            }
            foreach (var originatorSession in _originatorSessionsDict.Values)
            {
                originatorSession.OnSessionTermination -= ProcessSessionTerminationAsync;
                tasks.Add(
                    originatorSession.TerminateAsync(TerminationMessage.TerminationMessageCode.SessionClosedTransferCancelledByOriginator)
                        .ContinueWith(async _ => await originatorSession.WaitAsync()));
            }
            Task.WhenAll(tasks).GetAwaiter().GetResult();
        }

        private async Task NotifySessionCreatedAsync(VaspSession session)
        {
            var @event = new SessionCreatedEvent(session.SessionId);

            await TriggerAsyncEvent(SessionCreated, @event);
        }

        private async Task ProcessSessionTerminationAsync(SessionTerminationEvent @event)
        {
            await TriggerAsyncEvent(SessionTerminated, @event);

            string sessionId = @event.SessionId;
            VaspSession vaspSession;

            if (!_beneficiarySessionsDict.TryRemove(sessionId, out var benSession))
            {
                if (!_originatorSessionsDict.TryRemove(sessionId, out var origSession))
                    return;

                vaspSession = origSession;
            }
            else
            {
                vaspSession = benSession;
            }

            //TODO: Work on session life cycle
            try
            {
                vaspSession.Dispose();
            }
            catch (Exception e)
            {
            }
        }

        private Task TriggerAsyncEvent<T>(Func<T, Task> eventDelegates, T @event)
        {
            if (eventDelegates == null)
                return Task.CompletedTask;

            var tasks = eventDelegates.GetInvocationList()
                .OfType<Func<T, Task>>()
                .Select(d => d(@event));
            return Task.WhenAll(tasks);
        }
    }
}