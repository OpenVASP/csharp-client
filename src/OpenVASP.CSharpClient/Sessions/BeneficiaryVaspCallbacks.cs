using System;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient.Sessions
{
    internal class BeneficiaryVaspCallbacks : IBeneficiaryVaspCallbacks
    {
        private readonly Func<TransferRequestMessage, VaspSession, Task> _transferRequest;
        private readonly Func<TransferDispatchMessage, VaspSession, Task> _transferDispatch;
        private readonly Func<SessionRequestMessage, VaspSession, Task> _sessionAuthorizeRequest;
        private readonly Func<TerminationMessage, VaspSession, Task> _termination;

        public BeneficiaryVaspCallbacks(
            Func<SessionRequestMessage, VaspSession, Task> sessionRequest,
            Func<TransferRequestMessage, VaspSession, Task> transferRequest,
            Func<TransferDispatchMessage, VaspSession, Task> transferDispatch,
            Func<TerminationMessage, VaspSession, Task> termination)
        {
            _sessionAuthorizeRequest = sessionRequest ?? throw new ArgumentNullException(nameof(sessionRequest));
            _transferRequest = transferRequest ?? throw new ArgumentNullException(nameof(transferRequest));
            _transferDispatch = transferDispatch ?? throw new ArgumentNullException(nameof(transferDispatch));
            _termination = termination ?? throw new ArgumentNullException(nameof(termination));
        }

        Task IBeneficiaryVaspCallbacks.SessionRequestHandlerAsync(SessionRequestMessage request, VaspSession vaspSession)
        {
            return _sessionAuthorizeRequest.Invoke(request, vaspSession);
        }

        Task IBeneficiaryVaspCallbacks.TransferRequestHandlerAsync(TransferRequestMessage request, VaspSession vaspSession)
        {
            return _transferRequest.Invoke(request, vaspSession);
        }

        Task IBeneficiaryVaspCallbacks.TransferDispatchHandlerAsync(TransferDispatchMessage request, VaspSession vaspSession)
        {
            return _transferDispatch.Invoke(request, vaspSession);
        }

        Task IBeneficiaryVaspCallbacks.TerminationHandlerAsync(TerminationMessage request, VaspSession vaspSession)
        {
            return _termination.Invoke(request, vaspSession);
        }
    }
}
