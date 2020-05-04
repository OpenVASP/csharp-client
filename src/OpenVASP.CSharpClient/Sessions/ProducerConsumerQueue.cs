using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient.Sessions
{
    public class ProducerConsumerQueue : IDisposable
    {
        private readonly MessageHandlerResolver _messageHandlerResolver;
        private readonly Queue<MessageBase> _bufferQueue = new Queue<MessageBase>();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly AutoResetEvent _manual = new AutoResetEvent(false);

        private Task _queueWorker;

        public ProducerConsumerQueue(MessageHandlerResolver messageHandlerResolver, CancellationToken cancellationToken)
        {
            this._messageHandlerResolver = messageHandlerResolver;
            this._cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            StartWorker();
        }

        public void Enqueue(MessageBase message)
        {
            _semaphore.Wait();

            _bufferQueue.Enqueue(message);

            _manual.Set();

            _semaphore.Release();
        }

        private void StartWorker()
        {
            var cancellationToken = _cancellationTokenSource.Token;
            var factory = new TaskFactory();
            _queueWorker = factory.StartNew(async _ =>
            {
                do
                {
                    _manual.WaitOne();

                    while (_bufferQueue.Any())
                    {
                        try
                        {
                            await _semaphore.WaitAsync(cancellationToken);
                            var item = _bufferQueue.Dequeue();
                            var handlers = _messageHandlerResolver.ResolveMessageHandlers(item.GetType());

                            foreach (var handler in handlers)
                            {
                                await handler.HandleMessageAsync(item, cancellationToken);
                            }
                        }
                        catch (Exception e)
                        {
                            //TODO: Add logging here
                            throw;
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    }

                } while (!cancellationToken.IsCancellationRequested);

            }, cancellationToken, TaskCreationOptions.LongRunning);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _manual.Set();

            try
            {
                _queueWorker.Wait();
            }
            catch
            {
                //TODO: process exception
                // ignored
            }

            _semaphore?.Dispose();
            _manual?.Dispose();
            _queueWorker?.Dispose();
        }
    }
}
