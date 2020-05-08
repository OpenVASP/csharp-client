using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Cryptography;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient
{
    /// <summary>
    /// Listener which would process incoming beneficiary session request messages
    /// </summary>
    internal class SessionsRequestsListener : IDisposable
    {
        private bool _hasStartedListening;
        private Task _listener;

        private readonly ECDH_Key _handshakeKey;
        private readonly string _signatureKey;
        private readonly VaspCode _vaspCode;
        private readonly VaspInformation _vaspInfo;
        private readonly IEthereumRpc _ethereumRpc;
        private readonly ITransportClient _transportClient;
        private readonly ISignService _signService;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly MessagesTimeoutsConfiguration _messagesTimeoutsConfiguration;
        private readonly object _lock = new object();

        /// <summary>
        /// Notifies about session creation.
        /// </summary>
        public event Func<BeneficiarySession, Task> SessionCreated;

        public SessionsRequestsListener(
            ECDH_Key handshakeKey,
            string signatureKey,
            VaspCode vaspCode,
            VaspInformation vaspInfo,
            IEthereumRpc ethereumRpc,
            ITransportClient transportClient,
            ISignService signService,
            MessagesTimeoutsConfiguration messagesTimeoutsConfiguration)
        {
            _handshakeKey = handshakeKey;
            _signatureKey = signatureKey;
            _vaspCode = vaspCode;
            _vaspInfo = vaspInfo;
            _ethereumRpc = ethereumRpc;
            _transportClient = transportClient;
            _signService = signService;
            _messagesTimeoutsConfiguration = messagesTimeoutsConfiguration;
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
                if (!_hasStartedListening)
                {
                    _hasStartedListening = true;
                    var token = _cancellationTokenSource.Token;
                    var taskFactory = new TaskFactory(_cancellationTokenSource.Token);

                    _listener = taskFactory.StartNew(async _ =>
                    {
                        var privateKeyId = await _transportClient.RegisterKeyPairAsync(_handshakeKey.PrivateKey);
                        string messageFilter = await _transportClient.CreateMessageFilterAsync(_vaspCode.Code, privateKeyId);

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

                                var session = new BeneficiarySession(
                                    _vaspInfo,
                                    sessionRequestMessage.Message.SessionId,
                                    sessionRequestMessage.HandShake.TopicA,
                                    originatorVaspContractInfo.SigningKey,
                                    sharedSecret,
                                    _signatureKey,
                                    callbacks,
                                    _transportClient,
                                    _signService,
                                    _messagesTimeoutsConfiguration);

                                await callbacks.AuthorizeSessionRequestAsync(sessionRequestMessage, session);

                                if (SessionCreated != null)
                                {
                                    var tasks = SessionCreated.GetInvocationList()
                                        .OfType<Func<BeneficiarySession, Task>>()
                                        .Select(d => d(session));
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
        public void Stop()
        {
            _cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            Stop();

            _listener?.Dispose();
            _listener = null;
        }
    }
}
