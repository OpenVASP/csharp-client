using System;
using System.Threading;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient.Sessions
{
    internal abstract class VaspSession : IDisposable
    {
        private readonly object _lock = new object();
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ISignService _signService;

        private bool _isListening;
        private ProducerConsumerQueue _producerConsumerQueue;
        private Task _task;

        protected readonly MessageHandlerResolverBuilder _messageHandlerResolverBuilder;
        protected readonly ITransportClient _transportClient;

        public VaspSessionInfo Info { get; }

        public VaspSession(
            VaspSessionInfo vaspSessionInfo,
            ITransportClient transportClient,
            ISignService signService)
        {
            Info = vaspSessionInfo;
            _messageHandlerResolverBuilder = new MessageHandlerResolverBuilder();
            _transportClient = transportClient;
            _signService = signService;
        }

        public void OpenChannel()
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
                    _producerConsumerQueue = new ProducerConsumerQueue(messageHandlerResolver, cancellationToken);

                    do
                    {
                        var messages = await _transportClient.GetSessionMessagesAsync(Info.MessageFilter);

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
                                Info.CounterPartyPublicSigningKey))
                            {
                                //TODO: Log this
                                continue;
                            }

                            _producerConsumerQueue.Enqueue(message.Message);
                        }
                    } while (!cancellationToken.IsCancellationRequested);
                }, cancellationToken, TaskCreationOptions.LongRunning);

                _isListening = true;
            }
        }

        public async Task CloseChannelAsync()
        {
            try
            {
                Dispose();

                await _task;

                _isListening = false;
            }
            catch (Exception e)
            {
                //todo: handle
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();

            _task?.Dispose();
            _producerConsumerQueue?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        private Task ProcessUnexpectedMessageAsync(MessageBase message, CancellationToken token)
        {
            // TODO log this;

            return Task.CompletedTask;
        }
    }
}