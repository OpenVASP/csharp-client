using System;
using System.Collections.Generic;
using System.Linq;
using OpenVASP.Messaging.MessagingEngine;

namespace OpenVASP.Messaging
{
    public class MessageHandlerResolver
    {
        private readonly Dictionary<Type, MessageHandlerBase[]> _handlersDict;

        public MessageHandlerResolver()
        {
            _handlersDict = new Dictionary<Type, MessageHandlerBase[]>();
        }

        internal MessageHandlerResolver(params (Type type, MessageHandlerBase handler)[] handlers)
        {
            _handlersDict = handlers
                .GroupBy(x => x.type, y => y.handler)
                .ToDictionary(group => group.Key, group => group.ToArray());
        }

        public MessageHandlerBase[] ResolveMessageHandlers(Type type)
        {
            _handlersDict.TryGetValue(type, out var handlers);

            return handlers;
        }
    }
}