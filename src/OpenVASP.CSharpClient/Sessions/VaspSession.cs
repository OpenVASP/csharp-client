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
        private readonly object _lock = new object();
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly string _counterPartyPubSigningKey;
        private readonly ISignService _signService;

        private bool _isActivated;
        private ProducerConsumerQueue _producerConsumerQueue;
        private Task _task;

        protected readonly string _sessionTopic;
        protected readonly string _sharedKey;
        protected readonly string _privateSigningKey;
        protected readonly MessageHandlerResolverBuilder _messageHandlerResolverBuilder;
        protected readonly VaspInformation _vaspInfo;
        protected readonly ITransportClient _transportClient;

        protected bool _hasReceivedTerminationMessage = false;
        protected string _sharedSymKeyId;

        public event Func<SessionTerminationEvent, Task> OnSessionTermination;

        public string SessionId { get; protected set; }
        public string SessionTopic => _sessionTopic;
        public string CounterPartyTopic { get; protected set; }
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
            _sessionTopic = TopicGenerator.GenerateSessionTopic();
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
                if (!_isActivated)
                {
                    var taskFactory = new TaskFactory(_cancellationTokenSource.Token);
                    var cancellationToken = _cancellationTokenSource.Token;

                    _task = taskFactory.StartNew(async _ =>
                    {
                        _sharedSymKeyId = await _transportClient.RegisterSymKeyAsync(_sharedKey);
                        var messageFilter = await _transportClient.CreateMessageFilterAsync(_sessionTopic, symKeyId: _sharedSymKeyId);
                        var messageHandlerResolver = _messageHandlerResolverBuilder.Build();
                        _producerConsumerQueue = new ProducerConsumerQueue(messageHandlerResolver, cancellationToken);

                        do
                        {
                            var messages = await _transportClient.GetSessionMessagesAsync(messageFilter);

                            if (messages != null &&
                                messages.Count != 0)
                            {
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

                                    await _producerConsumerQueue.EnqueueAsync(message.Message);
                                }

                                continue;
                            }

                            //Poll whisper each 5 sec for new messages
                            await Task.Delay(5000, cancellationToken);

                        } while (!cancellationToken.IsCancellationRequested);
                    }, cancellationToken, TaskCreationOptions.LongRunning);

                    _isActivated = true;
                }
                else
                {
                    throw new Exception("Session was already started");
                }
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
    }
}