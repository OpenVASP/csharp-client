using System;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient.Sessions
{
    internal class OriginatorVaspCallbacks : IOriginatorVaspCallbacks
    {
        private readonly Func<SessionReplyMessage, VaspSession, Task> _sessionReply;
        private readonly Func<TransferReplyMessage, VaspSession, Task> _transferReply;
        private readonly Func<TransferConfirmationMessage, VaspSession, Task> _transferConfirm;

        public OriginatorVaspCallbacks(
            Func<SessionReplyMessage, VaspSession, Task> sessionReply,
            Func<TransferReplyMessage, VaspSession, Task> transferReply,
            Func<TransferConfirmationMessage, VaspSession, Task> transferConfirm)
        {
            _sessionReply = sessionReply ?? throw new ArgumentNullException(nameof(sessionReply));
            _transferReply = transferReply ?? throw new ArgumentNullException(nameof(transferReply));
            _transferConfirm = transferConfirm ?? throw new ArgumentNullException(nameof(transferConfirm));
        }

        public Task SessionReplyMessageHandlerAsync(SessionReplyMessage message, VaspSession session)
        {
            return _sessionReply.Invoke(message, session);
        }

        public Task TransferReplyMessageHandlerAsync(TransferReplyMessage message, VaspSession session)
        {
            return _transferReply.Invoke(message, session);
        }

        public Task TransferConfirmationMessageHandlerAsync(TransferConfirmationMessage message, VaspSession session)
        {
            return _transferConfirm.Invoke(message, session);
        }
    }
}