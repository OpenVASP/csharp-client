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
        
        private bool _isInCancellation = false;
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

            // Skip new messages if in disposing state
            if (!_isInCancellation)
                _bufferQueue.Enqueue(message);

            _manual.Set();

            _semaphore.Release();
        }

        public void Wait()
        {
            try
            {
                _queueWorker.Wait();
            }
            catch (Exception e)
            {
            }
        }

        private void StartWorker()
        {
            var cancellationToken = _cancellationTokenSource.Token;
            // ReSharper disable once MethodSupportsCancellation
            _queueWorker = Task.Run(async () => await DoWorkAsync(cancellationToken));
        }

        private async Task DoWorkAsync(CancellationToken cancellationToken)
        {
            do
            {
                _manual.WaitOne();

                await ProcessMessages(default);
            } while (!cancellationToken.IsCancellationRequested);

            // TODO: When disposing producer consumer queue 
            await ProcessMessages(default);
        }

        private async Task ProcessMessages(CancellationToken cancellationToken)
        {
            while (_bufferQueue.Any())
            {
                try
                {
                    await _semaphore.WaitAsync();
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
        }

        public void Dispose()
        {
            _isInCancellation = true;
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

            _queueWorker?.Dispose();
            _semaphore?.Dispose();
            _manual?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}
