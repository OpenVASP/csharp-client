using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient.Persistence
{
    public class InMemoryMessageRepository : IMessageRepository
    {
        private readonly Dictionary<Type, Func<MessageBase, string>> _supportedTypes = new Dictionary<Type, Func<MessageBase, string>>()
        {
            {typeof(SessionRequestMessage), (message) => ((SessionRequestMessage)message).Message.SessionId},
            {typeof(SessionReplyMessage),(message) => ((SessionReplyMessage)message).Message.SessionId},
            {typeof(TransferRequestMessage),(message) => ((TransferRequestMessage)message).Message.SessionId},
            {typeof(TransferReplyMessage),(message) => ((TransferReplyMessage)message).Message.SessionId},
            {typeof(TransferDispatchMessage),(message) => ((TransferDispatchMessage)message).Message.SessionId},
            {typeof(TransferConfirmationMessage),(message) => ((TransferConfirmationMessage)message).Message.SessionId},
            {typeof(TerminationMessage),(message) => ((TerminationMessage)message).Message.SessionId}
        };

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        
        public Dictionary<string, List<MessageBase>> MessageStorage { get; } = new Dictionary<string, List<MessageBase>>();

        public async Task SaveMessageAsync(MessageBase message)
        {
            if (!_supportedTypes.TryGetValue(message.GetType(), out var func))
            {
                return;
            }

            await _semaphore.WaitAsync();
            var sessionId = func(message);
            
            if (!MessageStorage.TryGetValue(sessionId, out var list))
            {
                list = new List<MessageBase>();

                MessageStorage[sessionId] = list;
            }

            list.Add(message);

            _semaphore.Release();
        }

        public async Task<IReadOnlyCollection<MessageBase>> ReadMessagesAsync(string sessionId, int start = 0, int take = 10)
        {
            await _semaphore.WaitAsync();
            if (MessageStorage.TryGetValue(sessionId, out var list))
            {
                return (IReadOnlyCollection<MessageBase>)list.Skip(start).Take(take).ToArray();
            }

            _semaphore.Release();

            return null;
        }
    }
}