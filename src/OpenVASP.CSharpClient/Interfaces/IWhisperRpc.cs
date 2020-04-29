using System.Collections.Generic;
using System.Threading.Tasks;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient.Interfaces
{
    public interface IWhisperRpc
    {
        /// <summary>
        /// Register SECP-256k1 private key
        /// </summary>
        /// <param name="privateKeyHex">privateKey in hex format</param>
        /// <returns>KeyPairId in whisper node</returns>
        Task<string> RegisterSymKeyAsync(string privateKeyHex);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="privateKeyHex"></param>
        /// <returns>SymKeyId in whisper node</returns>
        Task<string> RegisterKeyPairAsync(string privateKeyHex);

        /// <summary>
        /// Create a message filter in whisper node.
        /// </summary>
        /// <param name="topicHex">topic in hex</param>
        /// <param name="privateKeyId"></param>
        /// <param name="symKeyId"></param>
        /// <returns>Filter id</returns>
        Task<string> CreateMessageFilterAsync(string topicHex, string privateKeyId = null, 
            string symKeyId = null, string signingKey = null);

        /// <summary>
        /// Send message via Whisper
        /// </summary>
        /// <param name="topic">Destination topic</param>
        /// <param name="encryptionKey">Encryption key</param>
        /// <param name="encryptionType">Encryption type</param>
        /// <param name="payload">Payload for sending</param>
        /// <returns></returns>
        Task<string> SendMessageAsync(
            string topic, 
            string encryptionKey, 
            EncryptionType encryptionType,
            string payload);

        /// <summary>
        /// Get pending messages from filter
        /// </summary>
        /// <param name="messageFilter">filter</param>
        /// <returns></returns>
        Task<IReadOnlyCollection<ReceivedMessage>> GetMessagesAsync(string messageFilter);
    }
}