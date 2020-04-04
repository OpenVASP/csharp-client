using System;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient
{
    public class VaspMessageHandlerCallbacks : IVaspMessageHandler
    {
        private readonly Func<TransferRequestMessage, VaspSession, Task<TransferReplyMessage>> _transferRequest;
        private readonly Func<TransferDispatchMessage, VaspSession, Task<TransferConfirmationMessage>> _transferDispatch;
        private readonly Func<VaspInformation, Task<bool>> _sessionAuthorizeRequest;

        public VaspMessageHandlerCallbacks(
            Func<VaspInformation, Task<bool>> sessionAuthorizeRequest,
            Func<TransferRequestMessage, VaspSession, Task<TransferReplyMessage>> transferRequest,
            Func<TransferDispatchMessage, VaspSession, Task<TransferConfirmationMessage>> transferDispatch)
        {
            _sessionAuthorizeRequest = sessionAuthorizeRequest ?? throw new ArgumentNullException(nameof(sessionAuthorizeRequest));
            _transferRequest = transferRequest ?? throw new ArgumentNullException(nameof(transferRequest));
            _transferDispatch = transferDispatch ?? throw new ArgumentNullException(nameof(transferDispatch));
        }

        Task<bool> IVaspMessageHandler.AuthorizeSessionRequestAsync(VaspInformation request)
        {
            return _sessionAuthorizeRequest.Invoke(request);
        }

        Task<TransferReplyMessage> IVaspMessageHandler.TransferRequestHandlerAsync(TransferRequestMessage request, VaspSession vaspSession)
        {
            return _transferRequest.Invoke(request, vaspSession);
        }

        Task<TransferConfirmationMessage> IVaspMessageHandler.TransferDispatchHandlerAsync(TransferDispatchMessage request, VaspSession vaspSession)
        {
            return _transferDispatch.Invoke(request, vaspSession);
        }
    }
}
