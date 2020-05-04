using System;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient
{
    public class VaspMessageHandlerCallbacks : IVaspMessageHandler
    {
        private readonly Func<TransferRequestMessage, VaspSession, Task> _transferRequest;
        private readonly Func<TransferDispatchMessage, VaspSession, Task> _transferDispatch;
        private readonly Func<SessionRequestMessage, VaspSession, Task> _sessionAuthorizeRequest;

        public VaspMessageHandlerCallbacks(
            Func<SessionRequestMessage, VaspSession, Task> sessionAuthorizeRequest,
            Func<TransferRequestMessage, VaspSession, Task> transferRequest,
            Func<TransferDispatchMessage, VaspSession, Task> transferDispatch)
        {
            _sessionAuthorizeRequest = sessionAuthorizeRequest ?? throw new ArgumentNullException(nameof(sessionAuthorizeRequest));
            _transferRequest = transferRequest ?? throw new ArgumentNullException(nameof(transferRequest));
            _transferDispatch = transferDispatch ?? throw new ArgumentNullException(nameof(transferDispatch));
        }

        Task IVaspMessageHandler.AuthorizeSessionRequestAsync(SessionRequestMessage request, VaspSession vaspSession)
        {
            return _sessionAuthorizeRequest.Invoke(request, vaspSession);
        }

        Task IVaspMessageHandler.TransferRequestHandlerAsync(TransferRequestMessage request, VaspSession vaspSession)
        {
            return _transferRequest.Invoke(request, vaspSession);
        }

        Task IVaspMessageHandler.TransferDispatchHandlerAsync(TransferDispatchMessage request, VaspSession vaspSession)
        {
            return _transferDispatch.Invoke(request, vaspSession);
        }
    }
}
