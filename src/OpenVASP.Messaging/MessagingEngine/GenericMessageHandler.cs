using System;
using System.Threading;
using System.Threading.Tasks;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.Messaging.MessagingEngine
{
    public class GenericMessageHandler<TMessage> : MessageHandlerBase
        where TMessage : class
    {
        private readonly Func<TMessage, CancellationToken, Task> _messageProcessFunc;

        public GenericMessageHandler(Func<TMessage, CancellationToken, Task> messageProcessFunc)
        {
            this._messageProcessFunc = messageProcessFunc;
        }

        public override async Task HandleMessageAsync(MessageBase message, CancellationToken cancellationToken)
        {
            var messageForProcessing = message as TMessage;
            if (messageForProcessing != null)
            {
                await _messageProcessFunc(messageForProcessing, cancellationToken);
            }
        }
    }
}