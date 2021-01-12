using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OpenVASP.CSharpClient.Internals.Messages;

namespace OpenVASP.CSharpClient.Internals.Interfaces
{
    public interface IMessageFormatterService
    {
        Task<(string, string)> GetPayloadAsync(
            string targetVaspId,
            string sessionId,
            MessageType messageType,
            string ecdhPk,
            JObject messageBody,
            string aesKeyHex);
        (Message, string) Deserialize(
            string payload,
            string aesKeyHex,
            string signingKey);
    }
}