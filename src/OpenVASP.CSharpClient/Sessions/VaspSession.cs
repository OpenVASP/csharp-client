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
using Timer = System.Timers.Timer;

namespace OpenVASP.CSharpClient.Sessions
{
    //TODO: Add thread safety + state machine
    internal abstract class VaspSession : IDisposable
    {
        private readonly object _lock = new object();
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly string _counterPartyPubSigningKey;
        private readonly ISignService _signService;
        private readonly string _sharedKey;

        private bool _isActivated;
        private bool _hasReceivedTerminationMessage;
        private ProducerConsumerQueue _producerConsumerQueue;
        private Task _task;

        protected readonly string _privateSigningKey;
        protected readonly MessageHandlerResolverBuilder _messageHandlerResolverBuilder;
        protected readonly VaspInformation _vaspInfo;
        protected readonly ITransportClient _transportClient;
        protected readonly Timer _timer = new Timer { AutoReset = false };

        protected string _sharedSymKeyId;
        protected int _retriesCount;

        internal SessionState State { get; set; }

        public event Func<SessionTerminationEvent, Task> OnSessionTermination;

        public string SessionId { get; protected set; }
        public string CounterPartyTopic { get; protected set; }
        public string SessionTopic { get; } = TopicGenerator.GenerateSessionTopic();
        public VaspSessionCounterparty CounterParty { get; } = new VaspSessionCounterparty();

        public VaspSession(
            VaspInformation vaspInfo,
            string counterPartyPubSigningKey,
            string sharedEncryptionKey,
            string privateSigningKey,
            ITransportClient transportClient,
            ISignService signService)
        {
            _vaspInfo = vaspInfo;
            _cancellationTokenSource = new CancellationTokenSource();
            _sharedKey = sharedEncryptionKey;
            _privateSigningKey = privateSigningKey;
            _counterPartyPubSigningKey = counterPartyPubSigningKey;
            _messageHandlerResolverBuilder = new MessageHandlerResolverBuilder();
            _transportClient = transportClient;
            _signService = signService;
        }

        protected void StartTopicMonitoring()
        {
            lock (_lock)
            {
                if (_isActivated)
                    throw new InvalidOperationException("Session was already started");

                var taskFactory = new TaskFactory(_cancellationTokenSource.Token);
                var cancellationToken = _cancellationTokenSource.Token;

                _task = taskFactory.StartNew(async _ =>
                {
                    var messageFilter = await _transportClient.CreateMessageFilterAsync(SessionTopic, symKeyId: _sharedSymKeyId);
                    _messageHandlerResolverBuilder.AddDefaultHandler(ProcessUnexpectedMessageAsync);
                    var messageHandlerResolver = _messageHandlerResolverBuilder.Build();
                    _producerConsumerQueue = new ProducerConsumerQueue(messageHandlerResolver, cancellationToken);

                    do
                    {
                        var messages = await _transportClient.GetSessionMessagesAsync(messageFilter);

                        if (messages == null || messages.Count == 0)
                        {
                            await Task.Delay(5000, cancellationToken);
                            continue;
                        }

                        foreach (var message in messages)
                        {
                            if (!_signService.VerifySign(
                                message.Payload,
                                message.Signature,
                                _counterPartyPubSigningKey))
                            {
                                //TODO: Log this
                                continue;
                            }

                            _producerConsumerQueue.Enqueue(message.Message);
                        }
                    } while (!cancellationToken.IsCancellationRequested);
                }, cancellationToken, TaskCreationOptions.LongRunning);

                _isActivated = true;
            }
        }

        public async Task WaitAsync()
        {
            try
            {
                await _task;
            }
            catch (Exception e)
            {
                // TODO log this
            }
        }

        public async Task TerminateAsync(TerminationMessage.TerminationMessageCode terminationMessageCode)
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

            _task?.Dispose();
            _producerConsumerQueue?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        protected Task<string> RegisterSymKeyAsync()
        {
            return _transportClient.RegisterSymKeyAsync(_sharedKey);
        }

        protected Task ProcessTerminationMessageAsync(TerminationMessage message, CancellationToken token)
        {
            _timer.Enabled = false;
            _hasReceivedTerminationMessage = true;

            return TerminateAsync(message.GetMessageCode());
        }

        private async Task TerminateStrategyAsync(TerminationMessage.TerminationMessageCode terminationMessageCode)
        {
            var terminationMessage = TerminationMessage.Create(
                SessionId,
                terminationMessageCode,
                _vaspInfo);

            await _transportClient.SendAsync(new MessageEnvelope
            {
                Topic = CounterPartyTopic,
                SigningKey = _privateSigningKey,
                EncryptionType = EncryptionType.Symmetric,
                EncryptionKey = _sharedSymKeyId
            }, terminationMessage);
        }

        private Task NotifyAboutTerminationAsync()
        {
            var @event = new SessionTerminationEvent(SessionId);

            if (OnSessionTermination == null)
                return Task.CompletedTask;

            var tasks = OnSessionTermination.GetInvocationList()
                .OfType<Func<SessionTerminationEvent, Task>>()
                .Select(d => d(@event));
            return Task.WhenAll(tasks);
        }

        private Task ProcessUnexpectedMessageAsync(MessageBase message, CancellationToken token)
        {
            // TODO log this;

            return Task.CompletedTask;
        }
    }
}