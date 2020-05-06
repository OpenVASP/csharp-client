using System.Threading.Tasks;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient.Sessions
{
    internal interface IOriginatorVaspCallbacks
    {
        Task SessionReplyMessageHandlerAsync(SessionReplyMessage message, OriginatorSession session);
        Task TransferReplyMessageHandlerAsync(TransferReplyMessage message, OriginatorSession session);
        Task TransferConfirmationMessageHandlerAsync(TransferConfirmationMessage message, OriginatorSession session);
    }
}