using System;
using System.Collections.Generic;
using System.Linq;
using OpenVASP.Messaging.MessagingEngine;

namespace OpenVASP.Messaging
{
    public class MessageHandlerResolver
    {
        private readonly Dictionary<Type, MessageHandlerBase[]> _handlersDict;
        private readonly MessageHandlerBase _defaultHandler;

        public MessageHandlerResolver()
        {
            _handlersDict = new Dictionary<Type, MessageHandlerBase[]>();
        }

        internal MessageHandlerResolver(
            (Type type, MessageHandlerBase handler)[] handlers,
            MessageHandlerBase defaultHandler = null)
        {
            _handlersDict = handlers
                .GroupBy(x => x.type, y => y.handler)
                .ToDictionary(group => group.Key, group => group.ToArray());
            _defaultHandler = defaultHandler;
        }

        public MessageHandlerBase[] ResolveMessageHandlers(Type type)
        {
            if (_handlersDict.TryGetValue(type, out var handlers))
                return handlers;

            if (_defaultHandler != null)
                return new [] { _defaultHandler };

            return Array.Empty<MessageHandlerBase>();
        }
    }
}