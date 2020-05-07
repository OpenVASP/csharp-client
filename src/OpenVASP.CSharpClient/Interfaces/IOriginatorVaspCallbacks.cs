using System.Threading.Tasks;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient.Interfaces
{
    internal interface IOriginatorVaspCallbacks
    {
        Task SessionReplyMessageHandlerAsync(SessionReplyMessage message, VaspSession session);
        Task TransferReplyMessageHandlerAsync(TransferReplyMessage message, VaspSession session);
        Task TransferConfirmationMessageHandlerAsync(TransferConfirmationMessage message, VaspSession session);
    }
}