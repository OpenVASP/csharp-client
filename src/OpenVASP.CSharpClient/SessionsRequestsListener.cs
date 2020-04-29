using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Cryptography;
using OpenVASP.CSharpClient.Delegates;
using OpenVASP.CSharpClient.Events;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient
{
    /// <summary>
    /// Listener which would process incoming beneficiary session request messages
    /// </summary>
    class SessionsRequestsListener : IDisposable
    {
        private bool _hasStartedListening = false;
        private Task _listener;

        private readonly ECDH_Key _handshakeKey;
        private readonly string _signatureKey;
        private readonly VaspCode _vaspCode;
        private readonly VaspInformation _vaspInfo;
        private readonly IEthereumRpc _ethereumRpc;
        private readonly ITransportClient _transportClient;
        private readonly ISignService _signService;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private readonly object _lock = new object();

        /// <summary>
        /// Notifies about session creation.
        /// </summary>
        public event EventHandler<BeneficiarySession> SessionCreated;

        public SessionsRequestsListener(
            ECDH_Key handshakeKey,
            string signatureKey,
            VaspCode vaspCode,
            VaspInformation vaspInfo,
            IEthereumRpc ethereumRpc,
            ITransportClient transportClient,
            ISignService signService)
        {
            this._handshakeKey = handshakeKey;
            this._signatureKey = signatureKey;
            this._vaspCode = vaspCode;
            this._vaspInfo = vaspInfo;
            this._ethereumRpc = ethereumRpc;
            this._transportClient = transportClient;
            this._signService = signService;
        }

        /// <summary>
        /// Run listener which would process incoming messages.
        /// </summary>
        /// <param name="messageHandler">Handler which authorizes originator's Vasp and processes
        /// Transfer Request and Transfer Dispatch Messages</param>
        public void StartTopicMonitoring(IVaspMessageHandler messageHandler)
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
                        var privateKeyId = await _transportClient.RegisterKeyPairAsync(this._handshakeKey.PrivateKey);
                        string messageFilter =
                            await _transportClient.CreateMessageFilterAsync(topicHex: _vaspCode.Code, privateKeyId);

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

                                    var originatorVaspContractInfo =
                                        await _ethereumRpc.GetVaspContractInfoAync(sessionRequestMessage.Vasp
                                            .VaspIdentity);

                                    if (!_signService.VerifySign(message.Payload, message.Signature,
                                        originatorVaspContractInfo.SigningKey))
                                        continue;

                                    var sharedSecret =
                                        this._handshakeKey.GenerateSharedSecretHex(sessionRequestMessage.HandShake
                                            .EcdhPubKey);

                                    var session = new BeneficiarySession(
                                        this._vaspInfo,
                                        sessionRequestMessage.Message.SessionId,
                                        sessionRequestMessage.HandShake.TopicA,
                                        originatorVaspContractInfo.SigningKey,
                                        sharedSecret,
                                        this._signatureKey,
                                        messageHandler,
                                        _transportClient,
                                        _signService);

                                    await messageHandler.AuthorizeSessionRequestAsync(sessionRequestMessage, session);

                                    SessionCreated?.Invoke(this, session);
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
        /// Stops listener for incoming session request messages
        /// </summary>
        public void Stop()
        {
            _cancellationTokenSource.Cancel();

            if (SessionCreated != null)
                foreach (var d in SessionCreated.GetInvocationList())
                    SessionCreated -= (d as EventHandler<BeneficiarySession>);
        }

        public void Dispose()
        {
            Stop();

            _listener?.Dispose();
            _listener = null;
        }
    }
}
