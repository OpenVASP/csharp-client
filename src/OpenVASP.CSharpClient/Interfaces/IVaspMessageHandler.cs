using System.Threading.Tasks;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient.Interfaces
{
    public interface IVaspMessageHandler
    {
        /// <summary>
        /// Authorize originator VASP.
        /// </summary>
        /// <param name="request">Info about originator vasp</param>
        /// <param name="vaspSession">Session which processes a request</param>
        /// <returns>Is originator vasp allowed to start a session</returns>
        Task AuthorizeSessionRequestAsync(SessionRequestMessage request, VaspSession vaspSession);

        /// <summary>
        /// Handle TransferRequestMessage from originator
        /// </summary>
        /// <param name="request">TransferRequestMessage</param>
        /// <param name="vaspSession">Session which processes a request</param>
        Task TransferRequestHandlerAsync(TransferRequestMessage request, VaspSession vaspSession);

        /// <summary>
        /// Handle TransferDispatchMessage from originator
        /// </summary>
        /// <param name="request">TransferDispatchMessage</param>
        /// <param name="vaspSession">Session which processes a request</param>
        Task TransferDispatchHandlerAsync(TransferDispatchMessage request, VaspSession vaspSession);
    }
}