using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;

[assembly: InternalsVisibleTo("OpenVASP.Tests")]

namespace OpenVASP.CSharpClient.Sessions
{
    internal class ProducerConsumerQueue : IDisposable
    {
        private readonly MessageHandlerResolver _messageHandlerResolver;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ChannelWriter<MessageBase> _writer;
        private readonly ChannelReader<MessageBase> _reader;
        private readonly ILogger<ProducerConsumerQueue> _logger;

        private Task _queueWorker;

        public ProducerConsumerQueue(
            MessageHandlerResolver messageHandlerResolver,
            CancellationToken cancellationToken,
            ILogger<ProducerConsumerQueue> logger)
        {
            _messageHandlerResolver = messageHandlerResolver;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var channel = Channel.CreateUnbounded<MessageBase>();
            _writer = channel.Writer;
            _reader = channel.Reader;
            _logger = logger;

            StartWorker();
        }

        public void Enqueue(MessageBase message)
        {
            while (!_writer.TryWrite(message))
            {
            }
        }

        private void StartWorker()
        {
            var cancellationToken = _cancellationTokenSource.Token;
            var factory = new TaskFactory();
            _queueWorker = factory.StartNew(async _ =>
            {
                do
                {
                    try
                    {
                        var item = await _reader.ReadAsync(cancellationToken);

                        var handlers = _messageHandlerResolver.ResolveMessageHandlers(item.GetType());

                        foreach (var handler in handlers)
                        {
                            await handler.HandleMessageAsync(item, cancellationToken);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to process new messages for ");
                        throw;
                    }

                } while (!cancellationToken.IsCancellationRequested);

            }, cancellationToken, TaskCreationOptions.LongRunning);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();

            try
            {
                _queueWorker.GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                // ignored
                _logger.LogError(e, "Dispose fail");
            }

            _queueWorker?.Dispose();
        }
    }
}
