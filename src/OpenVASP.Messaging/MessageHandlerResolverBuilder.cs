using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.MessagingEngine;

namespace OpenVASP.Messaging
{
    public class MessageHandlerResolverBuilder
    {
        private readonly List<(Type type, MessageHandlerBase handler)> _registeredHandlers;
        private GenericMessageHandler<MessageBase> _defaultHandler;

        public MessageHandlerResolverBuilder()
        {
            _registeredHandlers = new List<(Type type, MessageHandlerBase handler)>();
        }

        public MessageHandlerResolverBuilder AddHandler<TMessage>(Func<TMessage, CancellationToken, Task> messageProcessFunc)
            where TMessage : MessageBase
        {
            _registeredHandlers.Add((typeof(TMessage), new GenericMessageHandler<TMessage>(messageProcessFunc)));

            return this;
        }

        public MessageHandlerResolverBuilder AddDefaultHandler(Func<MessageBase, CancellationToken, Task> messageProcessFunc)
        {
            _defaultHandler = new GenericMessageHandler<MessageBase>(messageProcessFunc);

            return this;
        }

        public MessageHandlerResolver Build()
        {
            var messageHandlerResolver = new MessageHandlerResolver(_registeredHandlers.ToArray(), _defaultHandler);

            return messageHandlerResolver;
        }
    }
}