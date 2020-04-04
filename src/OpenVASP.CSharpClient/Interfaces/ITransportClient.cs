using System.Collections.Generic;
using System.Threading.Tasks;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient.Interfaces
{
    public interface ITransportClient
    {
        /// <summary>
        /// Send a message via underlying transport.
        /// </summary>
        /// <param name="messageEnvelope">Instructions for destination and encryption</param>
        /// <param name="message">Message for sending</param>
        /// <returns>Id of the message</returns>
        Task<string> SendAsync(MessageEnvelope messageEnvelope, MessageBase message);

        /// <summary>
        /// Get messages from underlying transport using message filter
        /// </summary>
        /// <param name="messageFilter">Message filter</param>
        /// <returns>Collection of received messages</returns>
        Task<IReadOnlyCollection<TransportMessage>> GetSessionMessagesAsync(string messageFilter);
    }
}