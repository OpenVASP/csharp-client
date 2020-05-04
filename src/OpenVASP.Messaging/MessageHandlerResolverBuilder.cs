using System;
using System.Collections.Generic;
using OpenVASP.Messaging.MessagingEngine;

namespace OpenVASP.Messaging
{
    public class MessageHandlerResolverBuilder
    {
        private readonly List<(Type type, MessageHandlerBase handler)> _registeredHandlers;

        public MessageHandlerResolverBuilder()
        {
            _registeredHandlers = new List<(Type type, MessageHandlerBase handler)>();
        }

        public MessageHandlerResolverBuilder AddHandler(Type type, MessageHandlerBase handler)
        {
            _registeredHandlers.Add((type, handler));

            return this;
        }

        public MessageHandlerResolver Build()
        {
            var messageHandlerResolver = new MessageHandlerResolver(_registeredHandlers.ToArray());

            return messageHandlerResolver;
        }
    }
}