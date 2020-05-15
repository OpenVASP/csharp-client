using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenVASP.CSharpClient.Cryptography;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.CSharpClient.Utils;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient
{
    /// <summary>
    /// Listener which would process incoming beneficiary session request messages
    /// </summary>
    internal class SessionsRequestsListener : IDisposable
    {
        private bool _isListening;
        private Task _task;
        private CancellationTokenSource _cancellationTokenSource;

        private readonly ECDH_Key _handshakeKey;
        private readonly string _signatureKey;
        private readonly VaspCode _vaspCode;
        private readonly IEthereumRpc _ethereumRpc;
        private readonly ITransportClient _transportClient;
        private readonly ISignService _signService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<SessionsRequestsListener> _logger;
        private readonly object _lock = new object();

        /// <summary>
        /// Notifies about session creation.
        /// </summary>
        public event Func<BeneficiarySession, SessionRequestMessage, Task> SessionCreated;

        public SessionsRequestsListener(
            ECDH_Key handshakeKey,
            string signatureKey,
            VaspCode vaspCode,
            IEthereumRpc ethereumRpc,
            ITransportClient transportClient,
            ISignService signService,
            ILoggerFactory loggerFactory)
        {
            _handshakeKey = handshakeKey;
            _signatureKey = signatureKey;
            _vaspCode = vaspCode;
            _ethereumRpc = ethereumRpc;
            _transportClient = transportClient;
            _signService = signService;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<SessionsRequestsListener>();
        }

        /// <summary>
        /// Run listener which would process incoming messages.
        /// </summary>
        /// <param name="callbacks">Handler which authorizes originator's Vasp and processes
        /// Transfer Request and Transfer Dispatch Messages</param>
        public void StartTopicMonitoring(IBeneficiaryVaspCallbacks callbacks)
        {
            lock (_lock)
            {
                if (!_isListening)
                {
                    _isListening = true;
                    _cancellationTokenSource = new CancellationTokenSource();
                    var token = _cancellationTokenSource.Token;
                    var taskFactory = new TaskFactory(_cancellationTokenSource.Token);

                    _task = taskFactory.StartNew(async _ =>
                    {
                        var privateKeyId = await _transportClient.RegisterKeyPairAsync(_handshakeKey.PrivateKey);
                        var messageFilter = await _transportClient.CreateMessageFilterAsync(_vaspCode.Code, privateKeyId);

                        do
                        {
                            var sessionRequestMessages = await _transportClient.GetSessionMessagesAsync(messageFilter);
                            if (sessionRequestMessages == null || sessionRequestMessages.Count == 0)
                            {
                                await Task.Delay(5000, token);
                                continue;
                            }

                            foreach (var message in sessionRequestMessages)
                            {
                                if (!(message.Message is SessionRequestMessage sessionRequestMessage))
                                    continue;

                                var originatorVaspContractInfo = await _ethereumRpc.GetVaspContractInfoAync(
                                    sessionRequestMessage.Vasp.VaspIdentity);

                                if (!_signService.VerifySign(
                                    message.Payload,
                                    message.Signature,
                                    originatorVaspContractInfo.SigningKey))
                                    continue;

                                var sharedSecret = _handshakeKey.GenerateSharedSecretHex(sessionRequestMessage.HandShake.EcdhPubKey);
                                var symKey = await _transportClient.RegisterSymKeyAsync(sharedSecret);
                                var topic = TopicGenerator.GenerateSessionTopic();
                                var filter = await _transportClient.CreateMessageFilterAsync(
                                    topic,
                                    symKeyId: symKey);

                                var sessionInfo = new BeneficiarySessionInfo
                                {
                                    Id = sessionRequestMessage.Message.SessionId,
                                    PrivateSigningKey = _signatureKey,
                                    SharedEncryptionKey = sharedSecret,
                                    CounterPartyPublicSigningKey = originatorVaspContractInfo.SigningKey,
                                    Topic = topic,
                                    CounterPartyTopic = sessionRequestMessage.HandShake.TopicA,
                                    MessageFilter = filter,
                                    SymKey = symKey
                                };

                                var session = new BeneficiarySession(
                                    sessionInfo,
                                    callbacks,
                                    _transportClient,
                                    _signService,
                                    _loggerFactory);

                                if (SessionCreated != null)
                                {
                                    var tasks = SessionCreated.GetInvocationList()
                                        .OfType<Func<BeneficiarySession, SessionRequestMessage, Task>>()
                                        .Select(d => d(session, sessionRequestMessage));
                                    await Task.WhenAll(tasks);
                                }
                            }
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
        /// Stops listener for incoming session request messages
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                _cancellationTokenSource?.Cancel();

                if (_task != null)
                    await _task;

                _isListening = false;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to stop");
            }
        }

        public void Dispose()
        {
            if (_isListening)
                StopAsync().GetAwaiter().GetResult();

            _cancellationTokenSource?.Cancel();

            _task?.Dispose();
            _task = null;
        }
    }
}
