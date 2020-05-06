using System;
using System.Threading.Tasks;
using OpenVASP.Messaging.Messages;

namespace OpenVASP.CSharpClient.Sessions
{
    internal class OriginatorVaspCallbacks : IOriginatorVaspCallbacks
    {
        private readonly Func<SessionReplyMessage, OriginatorSession, Task> _sessionReply;
        private readonly Func<TransferReplyMessage, OriginatorSession, Task> _transferReply;
        private readonly Func<TransferConfirmationMessage, OriginatorSession, Task> _transferConfirm;

        public OriginatorVaspCallbacks(
            Func<SessionReplyMessage, OriginatorSession, Task> sessionReply,
            Func<TransferReplyMessage, OriginatorSession, Task> transferReply,
            Func<TransferConfirmationMessage, OriginatorSession, Task> transferConfirm)
        {
            _sessionReply = sessionReply ?? throw new ArgumentNullException(nameof(sessionReply));
            _transferReply = transferReply ?? throw new ArgumentNullException(nameof(transferReply));
            _transferConfirm = transferConfirm ?? throw new ArgumentNullException(nameof(transferConfirm));
        }

        public Task SessionReplyMessageHandlerAsync(SessionReplyMessage message, OriginatorSession session)
        {
            return _sessionReply.Invoke(message, session);
        }

        public Task TransferReplyMessageHandlerAsync(TransferReplyMessage message, OriginatorSession session)
        {
            return _transferReply.Invoke(message, session);
        }

        public Task TransferConfirmationMessageHandlerAsync(TransferConfirmationMessage message, OriginatorSession session)
        {
            return _transferConfirm.Invoke(message, session);
        }
    }
}