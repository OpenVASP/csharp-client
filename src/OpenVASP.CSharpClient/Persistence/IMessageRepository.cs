using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient.Persistence
{
    public interface IMessageRepository
    {
        Task SaveMessageAsync(MessageBase message);

        Task<IReadOnlyCollection<MessageBase>> ReadMessagesAsync(string sessionId, int start = 0, int take = 10);
    }
}
