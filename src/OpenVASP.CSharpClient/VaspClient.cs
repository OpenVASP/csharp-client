using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        private readonly ILoggerFactory _logFactory;

        /// <summary>Notifies about beneficiary session creation.</summary>
        public event Func<BeneficiarySessionCreatedEvent, Task> BeneficiarySessionCreated;
        /// <summary>Notifies about received session request message.</summary>
        public event Func<SessionMessageEvent<SessionRequestMessage>, Task> SessionRequestMessageReceived;
        /// <summary>Notifies about received session reply message.</summary>
        public event Func<SessionMessageEvent<SessionReplyMessage>, Task> SessionReplyMessageReceived;
        /// <summary>Notifies about received transfer request message.</summary>
        public event Func<SessionMessageEvent<TransferRequestMessage>, Task> TransferRequestMessageReceived;
        /// <summary>Notifies about received transfer reply message.</summary>
        public event Func<SessionMessageEvent<TransferReplyMessage>, Task> TransferReplyMessageReceived;
        /// <summary>Notifies about received transfer dispatch message.</summary>
        public event Func<SessionMessageEvent<TransferDispatchMessage>, Task> TransferDispatchMessageReceived;
        /// <summary>Notifies about received transfer confirmation message.</summary>
        public event Func<SessionMessageEvent<TransferConfirmationMessage>, Task> TransferConfirmationMessageReceived;
        /// <summary>Notifies about received termination message.</summary>
        public event Func<SessionMessageEvent<TerminationMessage>, Task> TerminationMessageReceived;

        /// <summary>
        /// VASP Code
        /// </summary>
        public VaspCode VaspCode { get; }

        public  VaspClient(
            ECDH_Key handshakeKey,
            string signatureHexKey,
            VaspCode vaspCode,
            IEthereumRpc nodeClientEthereumRpc,
            IEnsProvider ensProvider,
            ITransportClient transportClient,
            ISignService signService,
            ILoggerFactory logFactory)
        {
            _signatureKey = signatureHexKey;
            VaspCode = vaspCode;
            _ethereumRpc = nodeClientEthereumRpc;
            _ensProvider = ensProvider;
            _transportClient = transportClient;
            _signService = signService;
            _logFactory = logFactory;

            _originatorVaspCallbacks = new OriginatorVaspCallbacks(
                async (message, session) =>
                {
                    await TriggerAsyncEvent(
                        SessionReplyMessageReceived,
                        new SessionMessageEvent<SessionReplyMessage>(session.Id, message));
                },
                async (message, session) =>
                {
                    await TriggerAsyncEvent(
                        TransferReplyMessageReceived,
                        new SessionMessageEvent<TransferReplyMessage>(session.Id, message));
                },
                async (message, session) =>
                {
                    await TriggerAsyncEvent(
                        TransferConfirmationMessageReceived,
                        new SessionMessageEvent<TransferConfirmationMessage>(session.Id, message));
                });

            _beneficiaryVaspCallbacks = new BeneficiaryVaspCallbacks(
                async (request, session) =>
                {
                    await TriggerAsyncEvent(
                        SessionRequestMessageReceived,
                        new SessionMessageEvent<SessionRequestMessage>(session.Id, request));
                },
                async (request, session) =>
                {
                    await TriggerAsyncEvent(
                        TransferRequestMessageReceived,
                        new SessionMessageEvent<TransferRequestMessage>(session.Id, request));
                },
                async (dispatch, session) =>
                {
                    await TriggerAsyncEvent(
                        TransferDispatchMessageReceived,
                        new SessionMessageEvent<TransferDispatchMessage>(session.Id, dispatch));
                },
                async (termination, session) =>
                {
                    await TriggerAsyncEvent(
                        TerminationMessageReceived,
                        new SessionMessageEvent<TerminationMessage>(session.Id, termination));
                });

            _sessionsRequestsListener = new SessionsRequestsListener(
                handshakeKey,
                signatureHexKey,
                vaspCode,
                nodeClientEthereumRpc,
                transportClient,
                signService,
                _logFactory);
            _sessionsRequestsListener.SessionCreated += BeneficiarySessionCreatedAsync;
            _sessionsRequestsListener.StartTopicMonitoring(_beneficiaryVaspCallbacks);
        }

        private async Task BeneficiarySessionCreatedAsync(BeneficiarySession session, SessionRequestMessage sessionRequestMessage)
        {
            _beneficiarySessionsDict.TryAdd(session.Id, session);

            session.OpenChannel();

            await NotifyBeneficiarySessionCreatedAsync(session);

            await session.ProcessSessionRequestMessageAsync(sessionRequestMessage);
        }

        public async Task CloseSessionAsync(string sessionId)
        {
            if (_originatorSessionsDict.TryRemove(sessionId, out var originatorSession))
                await originatorSession.CloseChannelAsync();
            else if (_beneficiarySessionsDict.TryRemove(sessionId, out var beneficiarySessionSession))
                await beneficiarySessionSession.CloseChannelAsync();
            else
                throw new ArgumentException($"Session with id {sessionId} not found");
        }

        public Task<BeneficiarySession> CreateBeneficiarySessionAsync(BeneficiarySessionInfo sessionInfo)
        {
            var session = new BeneficiarySession(
                sessionInfo,
                _beneficiaryVaspCallbacks,
                _transportClient,
                _signService,
                _logFactory);

            session.OpenChannel();

            _beneficiarySessionsDict.TryAdd(session.Id, session);

            return Task.FromResult(session);
        }

        public async Task<OriginatorSession> CreateOriginatorSessionAsync(VaspCode vaspCode, OriginatorSessionInfo sessionInfo = null)
        {
            if (sessionInfo == null)
            {
                sessionInfo = await GenerateOriginatorSessionInfoAsync(vaspCode);
            }

            var session = new OriginatorSession(
                sessionInfo,
                vaspCode,
                _transportClient,
                _signService,
                _originatorVaspCallbacks,
                _logFactory);

            session.OpenChannel();

            _originatorSessionsDict.TryAdd(session.Id, session);

            return session;
        }

        public static VaspClient Create(
            VaspCode vaspCode,
            string handshakePrivateKeyHex,
            string signaturePrivateKeyHex,
            IEthereumRpc nodeClientEthereumRpc,
            IEnsProvider ensProvider,
            ISignService signService,
            ITransportClient transportClient,
            ILoggerFactory logFactory)
        {
            var handshakeKey = ECDH_Key.ImportKey(handshakePrivateKeyHex);

            var vaspClient = new VaspClient(
                handshakeKey,
                signaturePrivateKeyHex,
                vaspCode,
                nodeClientEthereumRpc,
                ensProvider,
                transportClient,
                signService,
                logFactory);

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
            var @event = new BeneficiarySessionCreatedEvent(session);

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

        private async Task<OriginatorSessionInfo> GenerateOriginatorSessionInfoAsync(VaspCode vaspCode)
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

            return new OriginatorSessionInfo
            {
                Id = Guid.NewGuid().ToByteArray().ToHex(true),
                PrivateSigningKey = _signatureKey,
                SharedEncryptionKey = sharedKey,
                CounterPartyPublicSigningKey = contractInfo.SigningKey,
                Topic = topic,
                PublicHandshakeKey = contractInfo.HandshakeKey,
                PublicEncryptionKey = sessionKey.PublicKey,
                MessageFilter = messageFilter,
                SymKey = symKey
            };
        }
    }
}