using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Shh.DTOs;
using Nethereum.Web3;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient
{
    public class WhisperRpc : IWhisperRpc
    {
        private readonly IWeb3 _web3;
        private readonly IMessageFormatter _messageFormatter;

        public WhisperRpc(IWeb3 web3, IMessageFormatter messageFormatter)
        {
            this._web3 = web3;
            this._messageFormatter = messageFormatter;
        }
        public async Task<string> RegisterSymKeyAsync(string privateKey)
        {
            var symKeyId = await _web3.Shh.SymKey.AddSymKey.SendRequestAsync(privateKey);

            return symKeyId;
        }

        public async Task<string> RegisterKeyPairAsync(string privateKey)
        {
            var keyPairId = await _web3.Shh.KeyPair.AddPrivateKey.SendRequestAsync(privateKey);

            return keyPairId;
        }

        public async Task<string> CreateMessageFilterAsync(string topic, string privateKeyId = null, string symKeyId = null, string signingKey = null)
        {
            var filter = await _web3.Shh.MessageFilter.NewMessageFilter.SendRequestAsync(new MessageFilterInput()
            {
                Topics = new Object [] {topic.EnsureHexPrefix()},
                PrivateKeyID = privateKeyId,
                SymKeyID = symKeyId,

            });
            
            return filter;
        }

        public async Task<string> SendMessageAsync(string topic, string encryptionKey, 
            EncryptionType encryptionType, string payload)
        {
            var messageInput = new MessageInput()
            {
                Topic = topic.EnsureHexPrefix(),
                Payload = payload,
                //TODO: Find a way to calculate it
                PowTime = 12,
                PowTarget = 0.4,
                Ttl = 300,
            };

            switch (encryptionType)
            {
                case EncryptionType.Assymetric:
                    messageInput.PubKey = encryptionKey;
                    break;
                case EncryptionType.Symmetric:
                    messageInput.SymKeyID = encryptionKey;
                    break;
                default:
                    throw new ArgumentException(
                        $"Current Encryption type {encryptionType} is not supported.",
                        nameof(encryptionType));
            }

            var messageHash = await _web3.Shh.Post.SendRequestAsync(messageInput);

            return messageHash;
        }

        public async Task<IReadOnlyCollection<SessionRequestMessage>> GetSessionRequestMessages(string messageFilter)
        {
            var messages = await GetSessionMessagesAsync(messageFilter);
            
            var sessionRequests = messages.Select(x =>
            {
                var sessionRequest = x as SessionRequestMessage;

                return sessionRequest;
            }).Where(x => x != null);

            return sessionRequests.ToArray();
        }

        public async Task<IReadOnlyCollection<MessageBase>> GetSessionMessagesAsync(string messageFilter)
        {
            var messages = await GetMessagesAsync(messageFilter);

            if (messages == null || messages.Count == 0)
            {
                return new MessageBase[] { };
            }

            var serializedMessages = messages.Select(x =>
            {
                var message = _messageFormatter.Deserialize(x.Payload);

                return message;
            }).ToArray();

            return serializedMessages;
        }

        public async Task<IReadOnlyCollection<ReceivedMessage>> GetMessagesAsync(string source)
        {
            try
            {
                var messages = await _web3.Shh.MessageFilter.GetFilterMessages.SendRequestAsync(source);

                if (messages == null || messages.Length == 0)
                {
                    return new ReceivedMessage[] { };
                }

                var receivedMessages = messages.Select(x => new ReceivedMessage
                {
                    MessageEnvelope = new MessageEnvelope
                    {
                        Topic = x.Topic,
                        EncryptionType = EncryptionType.Assymetric,
                        EncryptionKey = x.RecipientPublicKey,
                        SigningKey = x.Sig
                    },
                    Payload = x.Payload
                }).ToArray();

                return receivedMessages;
            }
            catch (Exception e)
            {
                return new ReceivedMessage[]{};
            }
        }
    }
}