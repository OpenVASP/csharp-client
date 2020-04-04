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
using OpenVASP.CSharpClient.Persistence;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

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
        private readonly IWhisperRpc _whisperRpc;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly IEnsProvider _ensProvider;
        private readonly ITransportClient _transportClient;
        private readonly ISignService _signService;
        private readonly IMessageRepository _messageRepository;

        private readonly ConcurrentDictionary<string, BeneficiarySession> _beneficiarySessionsDict = new ConcurrentDictionary<string, BeneficiarySession>();
        private readonly ConcurrentDictionary<string, OriginatorSession> _originatorSessionsDict = new ConcurrentDictionary<string, OriginatorSession>();

        private readonly string _signatureKey;
        private readonly ECDH_Key _handshakeKey;

        private readonly object _lock = new object();

        private bool _hasStartedListening = false;
        private Task _listener;
        private VaspContractInfo _vaspContractInfo;


        //TODO: Get rid of Whisper completely
        private VaspClient(
            ECDH_Key handshakeKey,
            string signatureHexKey,
            VaspContractInfo vaspContractInfo,
            VaspInformation vaspInfo,
            IEthereumRpc nodeClientEthereumRpc,
            IWhisperRpc nodeClientWhisperRpc,
            IEnsProvider ensProvider,
            ITransportClient transportClient,
            ISignService signService,
            IMessageRepository messageRepository)
        {
            this._handshakeKey = handshakeKey;
            this._signatureKey = signatureHexKey;
            this._vaspContractInfo = vaspContractInfo;
            this.VaspInfo = vaspInfo;
            this._ethereumRpc = nodeClientEthereumRpc;
            this._whisperRpc = nodeClientWhisperRpc;
            this._cancellationTokenSource = new CancellationTokenSource();
            this._ensProvider = ensProvider;
            this._transportClient = transportClient;
            this._signService = signService;
            this._messageRepository = messageRepository;
        }

        public VaspInformation VaspInfo { get; }

        /// <summary>
        /// Notifies about session termination.
        /// </summary>
        public event SessionTermination SessionTerminated;


        /// <summary>
        /// Notifies about session creation.
        /// </summary>
        public event SessionCreation SessionCreated;


        /// <summary>
        /// Run listener which would process incoming messages.
        /// </summary>
        /// <param name="messageHandler">Handler which authorizes originator's Vasp and processes Transfer Request and Transfer Dispatch Messages</param>
        public void RunListener(IVaspMessageHandler messageHandler)
        {
            lock (_lock)
            {
                if (!_hasStartedListening)
                {
                    _hasStartedListening = true;
                    var token = _cancellationTokenSource.Token;
                    var taskFactory = new TaskFactory(_cancellationTokenSource.Token);

                    this._listener = taskFactory.StartNew(async (_) =>
                    {
                        var privateKeyId = await _whisperRpc.RegisterKeyPairAsync(this._handshakeKey.PrivateKey);
                        string messageFilter =
                            await _whisperRpc.CreateMessageFilterAsync(topicHex: _vaspContractInfo.VaspCode.Code,
                                privateKeyId);

                        do
                        {
                            var sessionRequestMessages = await _transportClient.GetSessionMessagesAsync(messageFilter);

                            if (sessionRequestMessages != null &&
                                sessionRequestMessages.Count != 0)
                            {
                                foreach (var message in sessionRequestMessages)
                                {
                                    var sessionRequestMessage = message.Message as SessionRequestMessage;

                                    if (sessionRequestMessage == null)
                                        continue;

                                    var isAuthorized =
                                        await messageHandler.AuthorizeSessionRequestAsync(sessionRequestMessage.VASP);

                                    if (!isAuthorized)
                                        continue;

                                    var originatorVaspContractInfo =
                                        await _ethereumRpc.GetVaspContractInfoAync(sessionRequestMessage.VASP
                                            .VaspIdentity);

                                    if (!_signService.VerifySign(message.Payload, message.Signature,
                                        originatorVaspContractInfo.SigningKey))
                                        continue;

                                    var sharedSecret =
                                        this._handshakeKey.GenerateSharedSecretHex(sessionRequestMessage.HandShake
                                            .EcdhPubKey);

                                    await _messageRepository.SaveMessageAsync(sessionRequestMessage);

                                    var session = new BeneficiarySession(
                                        originatorVaspContractInfo,
                                        this.VaspInfo,
                                        sessionRequestMessage.Message.SessionId,
                                        sessionRequestMessage.HandShake.TopicA,
                                        originatorVaspContractInfo.SigningKey,
                                        sharedSecret,
                                        this._signatureKey,
                                        this._whisperRpc,
                                        messageHandler,
                                        _transportClient,
                                        _signService,
                                        _messageRepository);

                                    this.NotifySessionCreated(session);
                                    session.OnSessionTermination += this.ProcessSessionTermination;

                                    if (_beneficiarySessionsDict.TryAdd(session.SessionId, session))
                                    {
                                        await session.StartAsync();
                                    }
                                    else
                                    {
                                        await session.TerminateAsync(TerminationMessage.TerminationMessageCode
                                            .SessionClosedTransferDeclinedByBeneficiaryVasp);
                                    }
                                }

                                continue;
                            }

                            await Task.Delay(5000, token);
                        } while (!token.IsCancellationRequested);
                    }, token, TaskCreationOptions.LongRunning);
                }
                else
                {
                    throw new Exception("You can start observation only once.");
                }
            }
        }

        /// <summary>
        /// Block current thread and wait till client is disposed.
        /// </summary>
        public void Wait()
        {
            try
            {
                _listener.Wait();
            }
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Create a session and send a request session message to beneficiary Vasp.
        /// </summary>
        /// <param name="originator">Information about a client who sends a transfer</param>
        /// <param name="beneficiaryVaan">Information about a receiver of the transfer</param>
        /// <returns>OriginatorSession through which transfer request and transfer dispatch should be requested.</returns>
        public async Task<OriginatorSession> CreateSessionAsync(
            Originator originator,
            VirtualAssetssAccountNumber beneficiaryVaan)
        {
            string counterPartyVaspContractAddress = await _ensProvider.GetContractAddressByVaspCodeAsync(beneficiaryVaan.VaspCode);
            var contractInfo = await _ethereumRpc.GetVaspContractInfoAync(counterPartyVaspContractAddress);
            var sessionKey = ECDH_Key.GenerateKey();
            var sharedKey = sessionKey.GenerateSharedSecretHex(contractInfo.HandshakeKey);

            var session = new OriginatorSession(
                originator,
                this._vaspContractInfo,
                this.VaspInfo,
                beneficiaryVaan,
                contractInfo.SigningKey,
                contractInfo.HandshakeKey,
                sharedKey,
                sessionKey.PublicKey,
                this._signatureKey,
                _whisperRpc,
                _transportClient,
                _signService,
                _messageRepository);

            if (_originatorSessionsDict.TryAdd(session.SessionId, session))
            {
                this.NotifySessionCreated(session);
                session.OnSessionTermination += this.ProcessSessionTermination;
                await session.StartAsync();

                return session;
            }

            await session.TerminateAsync(TerminationMessage.TerminationMessageCode
                .SessionClosedTransferCancelledByOriginator);
            
            //TODO: process it as exception or retry
            return null;
        }

        public static VaspClient Create(
            VaspInformation vaspInfo,
            VaspContractInfo vaspContractInfo,
            string handshakePrivateKeyHex,
            string signaturePrivateKeyHex,
            IEthereumRpc nodeClientEthereumRpc,
            IWhisperRpc nodeClientWhisperRpc,
            IEnsProvider ensProvider,
            ISignService signService,
            ITransportClient transportClient,
            IMessageRepository messageRepository)
        {
            var handshakeKey = ECDH_Key.ImportKey(handshakePrivateKeyHex);

            var vaspClient = new VaspClient(
                handshakeKey,
                signaturePrivateKeyHex,
                vaspContractInfo,
                vaspInfo,
                nodeClientEthereumRpc,
                nodeClientWhisperRpc,
                ensProvider,
                transportClient,
                signService,
                messageRepository);

            return vaspClient;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();

            //TODO: Work on session life cycle

            var sessions = _beneficiarySessionsDict.Values.Cast<VaspSession>().Concat(_originatorSessionsDict.Values);

            foreach (var session in sessions)
            {
                bool isOriginator = session is OriginatorSession;
                var runningSession = session;
                runningSession
                    .TerminateAsync(isOriginator ? TerminationMessage.TerminationMessageCode.SessionClosedTransferCancelledByOriginator
                        : TerminationMessage.TerminationMessageCode.SessionClosedTransferDeclinedByBeneficiaryVasp).Wait();
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