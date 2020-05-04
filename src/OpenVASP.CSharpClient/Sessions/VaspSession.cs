using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Events;
using OpenVASP.CSharpClient.Interfaces;
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
        protected readonly CancellationTokenSource _cancellationTokenSource;
        protected readonly string _sessionTopic;
        protected readonly string _sharedKey;
        protected readonly string _privateSigningKey;
        protected readonly string _counterPartyPubSigningKey;
        protected readonly MessageHandlerResolverBuilder _messageHandlerResolverBuilder;
        protected readonly VaspInformation _vaspInfo;
        protected readonly ITransportClient _transportClient;
        protected readonly ISignService _signService;

        private readonly object _lock = new object();

        protected bool _hasReceivedTerminationMessage = false;
        protected string _sharedSymKeyId;
        protected Task _task;

        private bool _isActivated;
        private ProducerConsumerQueue _producerConsumerQueue;

        public event Func<SessionTerminationEvent, Task> OnSessionTermination;

        public string SessionId { get; protected set; }
        public string SessionTopic => _sessionTopic;
        public string CounterPartyTopic { get; protected set; }
        public VaspSessionCounterparty CounterParty { get; protected set; } = new VaspSessionCounterparty();

        public VaspSession(
            VaspInformation vaspInfo,
            string counterPartyPubSigningKey,
            string sharedEncryptionKey,
            string privateSigningKey,
            ITransportClient transportClient,
            ISignService signService)
        {
            this._vaspInfo = vaspInfo;
            this._sessionTopic = TopicGenerator.GenerateSessionTopic();
            this._cancellationTokenSource = new CancellationTokenSource();
            this._sharedKey = sharedEncryptionKey;
            this._privateSigningKey = privateSigningKey;
            this._counterPartyPubSigningKey = counterPartyPubSigningKey;
            this._messageHandlerResolverBuilder = new MessageHandlerResolverBuilder();
            this._transportClient = transportClient;
            this._signService = signService;
        }

        protected void StartTopicMonitoring()
        {
            lock (_lock)
            {
                if (!this._isActivated)
                {
                    var taskFactory = new TaskFactory(_cancellationTokenSource.Token);
                    this._isActivated = true;
                    var cancellationToken = _cancellationTokenSource.Token;

                    _task = taskFactory.StartNew(async (_) =>
                    {
                        _sharedSymKeyId = await _transportClient.RegisterSymKeyAsync(_sharedKey);
                        var messageFilter = await _transportClient.CreateMessageFilterAsync(topicHex: _sessionTopic, 
                            symKeyId: _sharedSymKeyId);
                        var messageHandlerResolver = _messageHandlerResolverBuilder.Build();
                        this._producerConsumerQueue = new ProducerConsumerQueue(messageHandlerResolver, cancellationToken);

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
                                    {
                                        //TODO: Log this
                                        continue;
                                    }

                                    _producerConsumerQueue.Enqueue(message.Message);
                                }

                                continue;
                            }

                            //Poll whisper each 5 sec for new messages
                            await Task.Delay(5000, cancellationToken);

                        } while (!cancellationToken.IsCancellationRequested);
                    }, cancellationToken, TaskCreationOptions.LongRunning);
                }
                else
                {
                    throw new Exception("Session was already started");
                }
            }
        }

        public void Wait()
        {
            try
            {
                _task.Wait();
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

            await NotifyAboutTerminationAsync();
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();

            if (OnSessionTermination != null)
            {
                var invocationList = OnSessionTermination?.GetInvocationList();

                foreach (var item in invocationList)
                {
                    OnSessionTermination -= (Func<SessionTerminationEvent, Task>)item;
                }
            }

            _task?.Dispose();
            _producerConsumerQueue?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        protected virtual async Task TerminateStrategyAsync(TerminationMessage.TerminationMessageCode terminationMessageCode)
        {
            var terminationMessage = TerminationMessage.Create(
                this.SessionId,
                terminationMessageCode,
                _vaspInfo);

            await _transportClient.SendAsync(new MessageEnvelope
            {
                Topic = this.CounterPartyTopic,
                SigningKey = _privateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = _sharedSymKeyId
            }, terminationMessage);
        }

        private Task NotifyAboutTerminationAsync()
        {
            var @event = new SessionTerminationEvent(this.SessionId);

            if (OnSessionTermination == null)
                return Task.CompletedTask;

            var tasks = OnSessionTermination.GetInvocationList()
                .OfType<Func<SessionTerminationEvent, Task>>()
                .Select(d => d(@event));
            return Task.WhenAll(tasks);
        }
    }
}