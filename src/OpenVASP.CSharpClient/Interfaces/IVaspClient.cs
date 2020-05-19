using System;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Events;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient.Interfaces
{
    /// <summary>
    /// Interface for the Vasp client
    /// </summary>
    public interface IVaspClient
    {
        /// <summary>Vasp code for this client</summary>
        VaspCode VaspCode { get; }

        /// <summary>Notifies about beneficiary session creation.</summary>
        event Func<BeneficiarySessionCreatedEvent, Task> BeneficiarySessionCreated;
        /// <summary>Notifies about received session request message.</summary>
        event Func<SessionMessageEvent<SessionRequestMessage>, Task> SessionRequestMessageReceived;
        /// <summary>Notifies about received session reply message.</summary>
        event Func<SessionMessageEvent<SessionReplyMessage>, Task> SessionReplyMessageReceived;
        /// <summary>Notifies about received transfer request message.</summary>
        event Func<SessionMessageEvent<TransferRequestMessage>, Task> TransferRequestMessageReceived;
        /// <summary>Notifies about received transfer reply message.</summary>
        event Func<SessionMessageEvent<TransferReplyMessage>, Task> TransferReplyMessageReceived;
        /// <summary>Notifies about received transfer dispatch message.</summary>
        event Func<SessionMessageEvent<TransferDispatchMessage>, Task> TransferDispatchMessageReceived;
        /// <summary>Notifies about received transfer confirmation message.</summary>
        event Func<SessionMessageEvent<TransferConfirmationMessage>, Task> TransferConfirmationMessageReceived;
        /// <summary>Notifies about received termination message.</summary>
        event Func<SessionMessageEvent<TerminationMessage>, Task> TerminationMessageReceived;

        /// <summary>Creates beneficiary session</summary>
        /// <param name="beneficiarySessionInfo">Beneficiary session information</param>
        /// <returns>Created beneficiary session</returns>
        Task<BeneficiarySession> CreateBeneficiarySessionAsync(BeneficiarySessionInfo beneficiarySessionInfo);

        /// <summary>Creates originator session</summary>
        /// <param name="benefeciaryVaspCode">Benefeciary VaspCode</param>
        /// <param name="originatorSessionInfo">Originator session information</param>
        /// <returns>Created originator session</returns>
        Task<OriginatorSession> CreateOriginatorSessionAsync(
            VaspCode benefeciaryVaspCode,
            OriginatorSessionInfo originatorSessionInfo = null);

        /// <summary>Closes created session</summary>
        /// <param name="sessionId">Session id</param>
        Task CloseSessionAsync(string sessionId);
    }
}