using System.Collections.Generic;
using System.Threading.Tasks;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient.Interfaces
{
    public interface ITransportClient
    {
        /// <summary>
        /// Register SECP-256k1 private key
        /// </summary>
        /// <param name="privateKeyHex">privateKey in hex format</param>
        /// <returns>Registered KeyPairId</returns>
        Task<string> RegisterSymKeyAsync(string privateKeyHex);

        /// <summary>
        /// Private key
        /// </summary>
        /// <param name="privateKeyHex"></param>
        /// <returns>Registered SymKeyId</returns>
        Task<string> RegisterKeyPairAsync(string privateKeyHex);

        /// <summary>
        /// Send a message via underlying transport.
        /// </summary>
        /// <param name="messageEnvelope">Instructions for destination and encryption</param>
        /// <param name="message">Message for sending</param>
        /// <returns>Id of the message</returns>
        Task<string> SendAsync(MessageEnvelope messageEnvelope, MessageBase message);

        /// <summary>
        /// Create a message filter in whisper node.
        /// </summary>
        /// <param name="topicHex">topic in hex</param>
        /// <param name="privateKeyId">private key id</param>
        /// <param name="symKeyId">sym key id</param>
        /// <param name="signingKey">signing key</param>
        /// <returns>Filter id</returns>
        Task<string> CreateMessageFilterAsync(string topicHex, string privateKeyId = null,
            string symKeyId = null, string signingKey = null);

        /// <summary>
        /// Get messages from underlying transport using message filter
        /// </summary>
        /// <param name="messageFilter">Message filter</param>
        /// <returns>Collection of received messages</returns>
        Task<IReadOnlyCollection<TransportMessage>> GetSessionMessagesAsync(string messageFilter);
    }
}