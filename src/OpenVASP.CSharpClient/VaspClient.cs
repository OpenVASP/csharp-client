using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Cryptography;
using OpenVASP.CSharpClient.Delegates;
using OpenVASP.CSharpClient.Events;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;
using OpenVASP.Tests.Client.Sessions;

namespace OpenVASP.CSharpClient
{
    //TODO: Add thread safety
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
        private readonly IVaspCallbacks _vaspCallbacks;
        private readonly SessionsRequestsListener _sessionsRequestsListener;

        private readonly ConcurrentDictionary<string, BeneficiarySession> _beneficiarySessionsDict = 
            new ConcurrentDictionary<string, BeneficiarySession>();
        private readonly ConcurrentDictionary<string, OriginatorSession> _originatorSessionsDict = 
            new ConcurrentDictionary<string, OriginatorSession>();

        private readonly string _signatureKey;
        private readonly ECDH_Key _handshakeKey;

        private readonly IOriginatorVaspCallbacks _originatorVaspCallbacks;

        /// <summary>
        /// Notifies about session termination.
        /// </summary>
        public event SessionTermination SessionTerminated;
        
        /// <summary>
        /// Notifies about session creation.
        /// </summary>
        public event SessionCreation SessionCreated;

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
            IVaspCallbacks vaspCallbacks)
        {
            this._handshakeKey = handshakeKey;
            this._signatureKey = signatureHexKey;
            this.VaspCode = vaspCode;
            this.VaspInfo = vaspInfo;
            this._ethereumRpc = nodeClientEthereumRpc;
            this._ensProvider = ensProvider;
            this._transportClient = transportClient;
            this._signService = signService;
            this._vaspCallbacks = vaspCallbacks;

            _originatorVaspCallbacks = new OriginatorVaspCallbacks(
                async (message, originatorSession) =>
                {
                    await vaspCallbacks.SessionReplyMessageReceivedAsync(originatorSession.SessionId, message);
                },
                async (message, originatorSession) =>
                {
                    await vaspCallbacks.TransferReplyMessageReceivedAsync(originatorSession.SessionId, message);
                    if (message.Message.MessageCode != "1") //todo: handle properly.
                    {
                        await originatorSession.TerminateAsync(TerminationMessage.TerminationMessageCode
                            .SessionClosedTransferOccured);
                        originatorSession.Wait();
                    }
                },
                async (message, originatorSession) =>
                {
                    await vaspCallbacks.TransferConfirmationMessageReceivedAsync(originatorSession.SessionId, message);
                    await originatorSession.TerminateAsync(TerminationMessage.TerminationMessageCode
                        .SessionClosedTransferOccured);
                    originatorSession.Wait();
                });

            IVaspMessageHandler vaspMessageHandler = new VaspMessageHandlerCallbacks(
                async (request, currentSession) =>
                {
                    _beneficiarySessionsDict[currentSession.SessionId] = currentSession as BeneficiarySession;
                    await vaspCallbacks.SessionRequestMessageReceivedAsync(currentSession.SessionId, request);
                },
                async (request, currentSession) =>
                {
                    await vaspCallbacks.TransferRequestMessageReceivedAsync(currentSession.SessionId, request);
                },
                async (dispatch, currentSession)
                    => await vaspCallbacks.TransferDispatchMessageReceivedAsync(currentSession.SessionId, dispatch));
            
            _sessionsRequestsListener = new SessionsRequestsListener(handshakeKey, signatureHexKey, vaspCode,
                vaspInfo, nodeClientEthereumRpc, transportClient, signService);
            _sessionsRequestsListener.SessionCreated += BeneficiarySessionCreated;
            _sessionsRequestsListener.StartTopicMonitoring(vaspMessageHandler);
        }

        private void BeneficiarySessionCreated(object sender, BeneficiarySession session)
        {
            _beneficiarySessionsDict.TryAdd(session.SessionId, session);
            session.OnSessionTermination += this.ProcessSessionTermination;
            this.NotifySessionCreated(session);
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
                this.VaspInfo,
                beneficiaryVaan,
                contractInfo.SigningKey,
                contractInfo.HandshakeKey,
                sharedKey,
                sessionKey.PublicKey,
                this._signatureKey,
                _transportClient,
                _signService,
                _originatorVaspCallbacks);

            await session.StartAsync();

            _originatorSessionsDict.TryAdd(session.SessionId, session);
            session.OnSessionTermination += this.ProcessSessionTermination;
            this.NotifySessionCreated(session);            

            return session.SessionId;
        }

        public async Task SessionReplyAsync(string sessionId, SessionReplyMessage.SessionReplyMessageCode code)
        {
            await _beneficiarySessionsDict[sessionId]
                .StartAsync(code);
        }

        public async Task TransferRequestAsync(string sessionId, string beneficiaryName, VirtualAssetType type,
            decimal amount)
        {
            await _originatorSessionsDict[sessionId]
                .TransferRequestAsync(
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
            await _beneficiarySessionsDict[sessionId].SendTransferReplyMessageAsync(message);
        }

        public async Task TransferDispatchAsync(string sessionId, TransferReply transferReply, string transactionHash,
            string sendingAddress, string beneficiaryName)
        {
            await _originatorSessionsDict[sessionId]
                .TransferDispatchAsync(
                    transferReply,
                    new Transaction(
                        transactionHash,
                        DateTime.UtcNow,
                        sendingAddress),
                    beneficiaryName);
        }

        public async Task TransferConfirmAsync(string sessionId, TransferConfirmationMessage message)
        {
            await _beneficiarySessionsDict[sessionId].SendTransferConfirmationMessageAsync(message);
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
            IVaspCallbacks vaspCallbacks)
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
                vaspCallbacks);

            return vaspClient;
        }

        public void Dispose()
        {
            _sessionsRequestsListener.Stop();

            var sessions = _beneficiarySessionsDict.Values.Cast<VaspSession>()
                .Concat(_originatorSessionsDict.Values);
            foreach (var session in sessions)
            {
                bool isOriginator = session is OriginatorSession;
                var runningSession = session;
                runningSession
                    .TerminateAsync(isOriginator
                        ? TerminationMessage.TerminationMessageCode.SessionClosedTransferCancelledByOriginator
                        : TerminationMessage.TerminationMessageCode.SessionClosedTransferDeclinedByBeneficiaryVasp)
                    .Wait();
                runningSession.Wait();
            }

            //if (TransferDispatch != null)
            //    foreach (var d in TransferDispatch.GetInvocationList())
            //        TransferDispatch -= (d as Func<TransferDispatchMessage, TransferConfirmationMessage>);

            //if (TransferRequest != null)
            //    foreach (var d in TransferRequest.GetInvocationList())
            //        TransferRequest -= (d as Func<TransferRequestMessage, TransferReplyMessage>);

            if (SessionTerminated != null)
                foreach (var d in SessionTerminated.GetInvocationList())
                    SessionTerminated -= (d as SessionTermination);

            if (SessionCreated != null)
                foreach (var d in SessionCreated.GetInvocationList())
                    SessionCreated -= (d as SessionCreation);
        }

        /// <summary>
        /// Get all active VaspSessions
        /// </summary>
        /// <returns>Active VaspSessions</returns>
        public IReadOnlyList<VaspSession> GetActiveSessions()
        {
            var beneficiarySessions = _beneficiarySessionsDict.Values.Cast<VaspSession>();
            var originatorSessions = _originatorSessionsDict.Values.Cast<VaspSession>();

            return beneficiarySessions.Concat(originatorSessions).ToArray();
        }

        private void NotifySessionCreated(VaspSession session)
        {
            var @event = new SessionCreatedEvent(session.SessionId);

            SessionCreated?.Invoke(@event);
        }

        private void ProcessSessionTermination(SessionTerminationEvent @event)
        {
            SessionTerminated?.Invoke(@event);

            string sessionId = @event.SessionId;
            VaspSession vaspSession;

            if (!_beneficiarySessionsDict.TryGetValue(sessionId, out var benSession))
            {
                if (!_originatorSessionsDict.TryGetValue(sessionId, out var origSession))
                {
                    return;
                }

                vaspSession = origSession;
                _originatorSessionsDict.TryRemove(sessionId, out _);
            }
            else
            {
                _beneficiarySessionsDict.TryRemove(sessionId, out _);
                vaspSession = benSession;
            }

            //TODO: Work on session life cycle
            try
            {
                vaspSession.Dispose();
            }
            finally
            {
            }
        }
    }
}