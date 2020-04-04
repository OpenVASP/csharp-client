using System.Threading.Tasks;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient.Interfaces
{
    public interface IVaspMessageHandler
    {
        /// <summary>
        /// Authorize originator VASP.
        /// </summary>
        /// <param name="request">Info about originator vasp</param>
        /// <returns>Is originator vasp allowed to start a session</returns>
        Task<bool> AuthorizeSessionRequestAsync(VaspInformation request);

        /// <summary>
        /// Handle TransferRequestMessage from originator
        /// </summary>
        /// <param name="request">TransferRequestMessage</param>
        /// <param name="vaspSession">Session which processes a request</param>
        /// <returns>TransferReplyMessage that will be sent to originator</returns>
        Task<TransferReplyMessage> TransferRequestHandlerAsync(TransferRequestMessage request, VaspSession vaspSession);

        /// <summary>
        /// Handle TransferDispatchMessage from originator
        /// </summary>
        /// <param name="request">TransferDispatchMessage</param>
        /// <param name="vaspSession">Session which processes a request</param>
        /// <returns>TransferConfirmationMessage</returns>
        Task<TransferConfirmationMessage> TransferDispatchHandlerAsync(TransferDispatchMessage request, VaspSession vaspSession);
    }
}