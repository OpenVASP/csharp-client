using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient.Sessions
{
    public abstract class VaspSession : IDisposable
    {
        private readonly object _lock = new object();
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ISignService _signService;
        private readonly VaspSessionInfo _info;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<VaspSession> _logger;

        private bool _isListening;
        private ProducerConsumerQueue _producerConsumerQueue;
        private Task _task;

        protected readonly MessageHandlerResolverBuilder _messageHandlerResolverBuilder;
        protected readonly ITransportClient _transportClient;

        public string Id => _info.Id;

        protected VaspSession(
            VaspSessionInfo vaspSessionInfo,
            ITransportClient transportClient,
            ISignService signService,
            ILoggerFactory loggerFactory)
        {
            _info = vaspSessionInfo;
            _messageHandlerResolverBuilder = new MessageHandlerResolverBuilder();
            _transportClient = transportClient;
            _signService = signService;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<VaspSession>();
        }

        public void Dispose()
        {
            if (_isListening)
                CloseChannelAsync().GetAwaiter().GetResult();

            _task?.Dispose();
            _task = null;

            _producerConsumerQueue?.Dispose();
            _producerConsumerQueue = null;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        internal async Task CloseChannelAsync()
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
                _logger.LogError(e, "Failed to close session channel");
            }
        }

        internal void OpenChannel()
        {
            lock (_lock)
            {
                if (_isListening)
                    throw new InvalidOperationException("Session was already started");

                _cancellationTokenSource = new CancellationTokenSource();

                var taskFactory = new TaskFactory(_cancellationTokenSource.Token);
                var cancellationToken = _cancellationTokenSource.Token;

                _task = taskFactory.StartNew(async _ =>
                {
                    _messageHandlerResolverBuilder.AddDefaultHandler(ProcessUnexpectedMessageAsync);
                    var messageHandlerResolver = _messageHandlerResolverBuilder.Build();
                    _producerConsumerQueue = new ProducerConsumerQueue(
                        messageHandlerResolver,
                        cancellationToken,
                        _loggerFactory.CreateLogger<ProducerConsumerQueue>());

                    do
                    {
                        var messages = await _transportClient.GetSessionMessagesAsync(_info.MessageFilter);

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
                                _info.CounterPartyPublicSigningKey))
                            {
                                _logger.LogWarning(
                                    $"Couldn't verify a message with payload {message.Payload} and signature {message.Signature}");
                                continue;
                            }

                            _producerConsumerQueue.Enqueue(message.Message);
                        }
                    } while (!cancellationToken.IsCancellationRequested);
                }, cancellationToken, TaskCreationOptions.LongRunning);

                _isListening = true;
            }
        }

        protected Task TriggerAsyncEvent<T>(Func<T, Task> eventDelegates, T @event)
        {
            if (eventDelegates == null)
                return Task.CompletedTask;

            var tasks = eventDelegates.GetInvocationList()
                .OfType<Func<T, Task>>()
                .Select(d => d(@event));
            return Task.WhenAll(tasks);
        }

        private Task ProcessUnexpectedMessageAsync(MessageBase message, CancellationToken token)
        {
            _logger.LogWarning($"Received unexpected message of type {message.GetType()}");

            return Task.CompletedTask;
        }
    }
}