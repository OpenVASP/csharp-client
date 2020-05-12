using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using OpenVASP.CSharpClient.Cryptography;
using OpenVASP.CSharpClient.Events;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.CSharpClient.Utils;
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
        private readonly IOriginatorVaspCallbacks _originatorVaspCallbacks;
        private readonly IBeneficiaryVaspCallbacks _beneficiaryVaspCallbacks;
        private readonly ConcurrentDictionary<string, BeneficiarySession> _beneficiarySessionsDict = 
            new ConcurrentDictionary<string, BeneficiarySession>();
        private readonly ConcurrentDictionary<string, OriginatorSession> _originatorSessionsDict = 
            new ConcurrentDictionary<string, OriginatorSession>();
        private readonly string _signatureKey;

        /// <summary>Notifies about beneficiary session creation.</summary>
        public event Func<BeneficiarySessionCreatedEvent, Task> BeneficiarySessionCreated;
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
        /// <summary>Notifies about received termination message.</summary>
        public event Func<SessionMessageEvent<TerminationMessage>, Task> TerminationMessageReceived;

        /// <summary>
        /// VASP Code
        /// </summary>
        public VaspCode VaspCode { get; }

        /// <summary>
        /// VASP Information
        /// </summary>
        public VaspInformation VaspInfo { get; }

        public  VaspClient(
            ECDH_Key handshakeKey,
            string signatureHexKey,
            VaspCode vaspCode,
            VaspInformation vaspInfo,
            IEthereumRpc nodeClientEthereumRpc,
            IEnsProvider ensProvider,
            ITransportClient transportClient,
            ISignService signService)
        {
            _signatureKey = signatureHexKey;
            VaspCode = vaspCode;
            VaspInfo = vaspInfo;
            _ethereumRpc = nodeClientEthereumRpc;
            _ensProvider = ensProvider;
            _transportClient = transportClient;
            _signService = signService;

            _originatorVaspCallbacks = new OriginatorVaspCallbacks(
                async (message, session) =>
                {
                    await TriggerAsyncEvent(
                        SessionReplyMessageReceived,
                        new SessionMessageEvent<SessionReplyMessage>(session.Info.Id, message));
                },
                async (message, session) =>
                {
                    await TriggerAsyncEvent(
                        TransferReplyMessageReceived,
                        new SessionMessageEvent<TransferReplyMessage>(session.Info.Id, message));
                },
                async (message, session) =>
                {
                    await TriggerAsyncEvent(
                        TransferConfirmationMessageReceived,
                        new SessionMessageEvent<TransferConfirmationMessage>(session.Info.Id, message));
                });

            _beneficiaryVaspCallbacks = new BeneficiaryVaspCallbacks(
                async (request, currentSession) =>
                {
                    await TriggerAsyncEvent(
                        SessionRequestMessageReceived,
                        new SessionMessageEvent<SessionRequestMessage>(currentSession.Info.Id, request));
                },
                async (request, currentSession) =>
                {
                    await TriggerAsyncEvent(
                        TransferRequestMessageReceived,
                        new SessionMessageEvent<TransferRequestMessage>(currentSession.Info.Id, request));
                },
                async (dispatch, currentSession) =>
                {
                    await TriggerAsyncEvent(
                        TransferDispatchMessageReceived,
                        new SessionMessageEvent<TransferDispatchMessage>(currentSession.Info.Id, dispatch));
                },
                async (termination, currentSession) =>
                {
                    await TriggerAsyncEvent(
                        TerminationMessageReceived,
                        new SessionMessageEvent<TerminationMessage>(currentSession.Info.Id, termination));
                });

            _sessionsRequestsListener = new SessionsRequestsListener(
                handshakeKey,
                signatureHexKey,
                vaspCode,
                nodeClientEthereumRpc,
                transportClient,
                signService);
            _sessionsRequestsListener.SessionCreated += BeneficiarySessionCreatedAsync;
            _sessionsRequestsListener.StartTopicMonitoring(_beneficiaryVaspCallbacks);
        }

        private async Task BeneficiarySessionCreatedAsync(BeneficiarySession session, SessionRequestMessage sessionRequestMessage)
        {
            _beneficiarySessionsDict.TryAdd(session.Info.Id, session);

            session.OpenChannel();

            await NotifyBeneficiarySessionCreatedAsync(session);

            await _beneficiaryVaspCallbacks.SessionRequestHandlerAsync(sessionRequestMessage, session);
        }

        public async Task CloseSessionAsync(string sessionId)
        {
            var session = (VaspSession) _originatorSessionsDict[sessionId] ?? _beneficiarySessionsDict[sessionId];

            if(session == null)
                throw new ArgumentException($"Session with id {sessionId} not found");

            _originatorSessionsDict.TryRemove(sessionId, out _);
            _beneficiarySessionsDict.TryRemove(sessionId, out _);

            await session.CloseChannelAsync();
        }

        public async Task<BeneficiarySessionInfo> CreateBeneficiarySessionAsync(BeneficiarySessionInfo sessionInfo)
        {
            var session = new BeneficiarySession(
                sessionInfo,
                _beneficiaryVaspCallbacks,
                _transportClient,
                _signService);

            _beneficiarySessionsDict.TryAdd(session.Info.Id, session);

            session.OpenChannel();

            return sessionInfo;
        }

        public async Task<OriginatorSessionInfo> CreateOriginatorSessionAsync(VaspCode vaspCode, OriginatorSessionInfo sessionInfo = null)
        {
            if (sessionInfo == null)
            {
                var counterPartyVaspContractAddress = await _ensProvider.GetContractAddressByVaspCodeAsync(vaspCode);
                var contractInfo = await _ethereumRpc.GetVaspContractInfoAync(counterPartyVaspContractAddress);
                var sessionKey = ECDH_Key.GenerateKey();
                var sharedKey = sessionKey.GenerateSharedSecretHex(contractInfo.HandshakeKey);
                var topic = TopicGenerator.GenerateSessionTopic();
                var symKey = await _transportClient.RegisterSymKeyAsync(sharedKey);
                var messageFilter = await _transportClient.CreateMessageFilterAsync(
                    topic,
                    symKeyId: symKey);

                sessionInfo = new OriginatorSessionInfo
                {
                    Id = Guid.NewGuid().ToByteArray().ToHex(true),
                    PrivateSigningKey = _signatureKey,
                    SharedEncryptionKey = sharedKey,
                    CounterPartyPublicSigningKey = contractInfo.SigningKey,
                    Topic = topic,
                    PublicHandshakeKey = contractInfo.HandshakeKey,
                    PublicEncryptionKey =  sessionKey.PublicKey,
                    MessageFilter = messageFilter,
                    SymKey = symKey
                };
            }

            var session = new OriginatorSession(
                sessionInfo,
                vaspCode,
                _transportClient,
                _signService,
                _originatorVaspCallbacks);

            session.OpenChannel();

            _originatorSessionsDict.TryAdd(session.Info.Id, session);

            return sessionInfo;
        }

        public async Task SessionRequestAsync(string sessionId)
        {
            if (!_originatorSessionsDict.TryGetValue(sessionId, out var session))
                throw new ArgumentException($"Originator session with id {sessionId} not found");
            
            await session.SessionRequestAsync(VaspInfo);
        }

        public async Task SessionReplyAsync(string sessionId, SessionReplyMessage.SessionReplyMessageCode code)
        {
            if (!_beneficiarySessionsDict.TryGetValue(sessionId, out var session))
                throw new ArgumentException($"Beneficiary session with id {sessionId} not found");

            await session.SessionReplyAsync(VaspInfo, code);
        }

        public async Task TerminateAsync(
            string sessionId,
            TerminationMessage.TerminationMessageCode code)
        {
            if (!_originatorSessionsDict.TryGetValue(sessionId, out var session))
                throw new ArgumentException($"Originator session with id {sessionId} not found");

            await session.TerminateAsync(code);
        }

        public async Task TransferRequestAsync(
            string sessionId,
            Originator originator,
            Beneficiary beneficiary,
            VirtualAssetType type,
            decimal amount)
        {
            if (!_originatorSessionsDict.TryGetValue(sessionId, out var session))
                throw new ArgumentException($"Originator session with id {sessionId} not found");

            await session.TransferRequestAsync(
                originator,
                beneficiary,
                new TransferInstruction
                {
                    VirtualAssetTransfer = new VirtualAssetTransfer
                    {
                        TransferType = TransferType.BlockchainTransfer,
                        VirtualAssetType = type,
                        TransferAmount = amount
                    }
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
            string transactionHash,
            string sendingAddress)
        {
            if (!_originatorSessionsDict.TryGetValue(sessionId, out var session))
                throw new ArgumentException($"Originator session with id {sessionId} not found");

            await session.TransferDispatchAsync(
                new Transaction(
                    transactionHash,
                    DateTime.UtcNow,
                    sendingAddress));
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
            ITransportClient transportClient)
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
                signService);

            return vaspClient;
        }

        public void Dispose()
        {
            _sessionsRequestsListener.StopAsync().GetAwaiter().GetResult();
            _sessionsRequestsListener.Dispose();

            foreach (var session in _originatorSessionsDict.Values)
            {
                session.Dispose();
            }
            _originatorSessionsDict.Clear();
            foreach (var session in _beneficiarySessionsDict.Values)
            {
                session.Dispose();
            }
            _beneficiarySessionsDict.Clear();
        }

        private async Task NotifyBeneficiarySessionCreatedAsync(BeneficiarySession session)
        {
            var @event = new BeneficiarySessionCreatedEvent(session.Info.Id, (BeneficiarySessionInfo) session.Info);

            await TriggerAsyncEvent(BeneficiarySessionCreated, @event);
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