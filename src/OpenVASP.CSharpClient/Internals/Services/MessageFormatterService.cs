using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenVASP.CSharpClient.Internals.Interfaces;
using OpenVASP.CSharpClient.Internals.Messages;
using OpenVASP.CSharpClient.Internals.Utils;

namespace OpenVASP.CSharpClient.Internals.Services
{
    public class MessageFormatterService : IMessageFormatterService
    {
        private readonly IMessageSignService _signService;
        private readonly string _vaspId;

        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public MessageFormatterService(
            IMessageSignService signService,
            string vaspId)
        {
            _signService = signService;
            _vaspId = vaspId;
        }

        public async Task<(string, string)> GetPayloadAsync(
            string targetVaspId,
            string sessionId,
            MessageType messageType,
            string ecdhPk,
            JObject messageBody,
            string aesKeyHex)
        {
            var messageContent = new MessageContent
            {
                Header = new MessageHeader(
                    _vaspId,
                    targetVaspId,
                    Guid.NewGuid().ToString("N"),
                    sessionId,
                    messageType,
                    ecdhPk),
                RawBody = messageBody,
            };
            var contentJson = JsonConvert.SerializeObject(messageContent, settings: JsonSerializerSettings);
            var message = new Message(messageContent)
            {
                Signature = await _signService.SignPayloadAsync(contentJson)
            };

            var json = JsonConvert.SerializeObject(message, settings: JsonSerializerSettings);

            var enryptionBytes = aesKeyHex.EnsureHexPrefix().HexToByteArray();
            var encrypted = json.ToHexUTF8().HexToByteArray().EncryptAesGcm(enryptionBytes).ToHex();

            return (encrypted, json);
        }

        public (Message, string) Deserialize(
            string payload,
            string aesKeyHex,
            string signingKey)
        {
            var decryptionBytes = aesKeyHex.EnsureHexPrefix().HexToByteArray();
            var decryptedJson = payload.HexToByteArray().DecryptAesGcm(decryptionBytes).ToHex().HexToUTF8String();

            var message = JsonConvert.DeserializeObject<Message>(decryptedJson);

            var contentJson = JsonConvert.SerializeObject(message.Content, settings: JsonSerializerSettings);

            if (!_signService.VerifySign(contentJson, message.Signature, signingKey))
                throw new InvalidOperationException($"Signature is not valid for message {JsonConvert.SerializeObject(message.Content.Header, settings: JsonSerializerSettings)}");

            return (message, decryptedJson);
        }
    }
}