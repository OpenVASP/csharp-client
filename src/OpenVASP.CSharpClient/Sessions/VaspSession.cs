using System;
using System.Threading;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Delegates;
using OpenVASP.CSharpClient.Events;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.CSharpClient.Persistence;
using OpenVASP.CSharpClient.Utils;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient.Sessions
{
    //TODO: Add thread safety + state machine
    public abstract class VaspSession : IDisposable
    {
        // ReSharper disable once NotAccessedField.Global
        protected readonly IWhisperRpc _whisperRpc;
        protected readonly CancellationTokenSource _cancellationTokenSource;
        protected readonly string _sessionTopic;
        protected readonly string _sharedKey;
        protected readonly string _privateSigningKey;
        protected readonly string _counterPartyPubSigningKey;
        protected readonly MessageHandlerResolverBuilder _messageHandlerResolverBuilder;
        protected readonly VaspInformation _vaspInfo;
        protected readonly VaspContractInfo _vaspContractInfo;
        protected readonly ITransportClient _transportClient;
        protected readonly ISignService _signService;

        private readonly object _lock = new object();

        protected bool _hasReceivedTerminationMessage = false;
        protected string _sharedSymKeyId;
        protected Task _task;

        private bool _isActivated;
        private ProducerConsumerQueue _producerConsumerQueue;
        private MessageHandlerResolver _messageHandlerResolver;

        public event SessionTermination OnSessionTermination;

        public VaspSession(
            VaspContractInfo vaspContractInfo,
            VaspInformation vaspInfo,
            string counterPartyPubSigningKey,
            string sharedEncryptionKey,
            string privateSigningKey,
            IWhisperRpc whisperRpc,
            ITransportClient transportClient,
            ISignService signService,
            IMessageRepository messageRepository)
        {
            this._vaspInfo = vaspInfo;
            this._vaspContractInfo = vaspContractInfo;
            this._sessionTopic = TopicGenerator.GenerateSessionTopic();
            this._cancellationTokenSource = new CancellationTokenSource();
            this._whisperRpc = whisperRpc;
            this._sharedKey = sharedEncryptionKey;
            this._privateSigningKey = privateSigningKey;
            this._counterPartyPubSigningKey = counterPartyPubSigningKey;
            this._messageHandlerResolverBuilder = new MessageHandlerResolverBuilder();
            this._transportClient = transportClient;
            this._signService = signService;
        }

        public string SessionId { get; protected set; }

        public string CounterPartyTopic { get; protected set; }
        public VaspSessionCounterparty CounterParty { get; protected set; } = new VaspSessionCounterparty();

        public string SessionTopic
        {
            get { return _sessionTopic; }
        }

        private void StartTopicMonitoring()
        {
            lock (_lock)
            {
                if (!this._isActivated)
                {
                    var taskFactory = new TaskFactory(_cancellationTokenSource.Token);
                    this._isActivated = true;
                    var cancellationToken = _cancellationTokenSource.Token;

                    _task = Task.Run(async () =>
                    {
                        _sharedSymKeyId = await _whisperRpc.RegisterSymKeyAsync(_sharedKey);
                        string messageFilter = await _whisperRpc.CreateMessageFilterAsync(topicHex: _sessionTopic, symKeyId: _sharedSymKeyId);
                        _messageHandlerResolver = _messageHandlerResolverBuilder.Build();
                        this._producerConsumerQueue = new ProducerConsumerQueue(_messageHandlerResolver, cancellationToken);

                        do
                        {
                            var messages = await _transportClient.GetSessionMessagesAsync(messageFilter);

                            if (messages != null &&
                                messages.Count != 0)
                            {
                                foreach (var message in messages)
                                {
                                    if (!_signService.VerifySign(message.Payload, message.Signature,
                                        _counterPartyPubSigningKey))
                                        continue;

                                    _producerConsumerQueue.Enqueue(message.Message);
                                }

                                continue;
                            }

                            //TODO: Enhance polling strategy
                            //Poll whisper each 5 sec for new messages
                            await Task.Delay(5000, cancellationToken);

                        } while (!cancellationToken.IsCancellationRequested);
                    }, cancellationToken);
                }
                else
                {
                    throw new Exception("Session was already started");
                }
            }
        }

        public virtual Task StartAsync()
        {
            StartTopicMonitoring();

            return Task.CompletedTask;
        }

        public void Wait()
        {
            try
            {
                _task.Wait();
                _producerConsumerQueue.Wait();
            }
            catch (Exception e)
            {
            }
        }


        public virtual async Task TerminateAsync(TerminationMessage.TerminationMessageCode terminationMessageCode)
        {
            if (!_hasReceivedTerminationMessage)
            {
                await TerminateStrategyAsync(terminationMessageCode);
            }

            NotifyAboutTermination();
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();

            if (OnSessionTermination != null)
            {
                var invocationList = OnSessionTermination?.GetInvocationList();
                
                foreach (var item in invocationList)
                {
                    OnSessionTermination -= (SessionTermination)item;
                }
            }

            Wait();

            _task?.Dispose();
            _producerConsumerQueue?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        protected virtual async Task TerminateStrategyAsync(TerminationMessage.TerminationMessageCode terminationMessageCode)
        {
            var terminationMessage = new TerminationMessage(
                this.SessionId,
                terminationMessageCode,
                _vaspInfo);

            await _transportClient.SendAsync(new MessageEnvelope()
            {
                Topic = this.CounterPartyTopic,
                SigningKey = _privateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = _sharedSymKeyId
            }, terminationMessage);
        }

        private void NotifyAboutTermination()
        {
            var @event = new SessionTerminationEvent(this.SessionId);

            OnSessionTermination?.Invoke(@event);
        }
    }
}