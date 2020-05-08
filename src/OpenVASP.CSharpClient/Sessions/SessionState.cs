namespace OpenVASP.CSharpClient.Sessions
{
    /// <summary>
    /// Session state enum
    /// </summary>
    public enum SessionState
    {
        /// <summary>Undefined state</summary>
        None = 0,
        /// <summary>SessionRequestMessage was sent</summary>
        SessionRequestSent,
        /// <summary>SessionRequestMessage was received</summary>
        SessionRequestReceived,
        /// <summary>SessionReplyMessage was sent</summary>
        SessionReplySent,
        /// <summary>SessionReplyMessage was received</summary>
        SessionReplyReceived,
        /// <summary>TransferRequestMessage was sent</summary>
        TransferRequestSent,
        /// <summary>TransferRequestMessage was received</summary>
        TransferRequestReceived,
        /// <summary>TransferReplyMessage was sent</summary>
        TransferReplySent,
        /// <summary>TransferReplyMessage was received</summary>
        TransferReplyReceived,
        /// <summary>TransferDispatchMessage was sent</summary>
        TransferDispatchSent,
        /// <summary>TransferDispatchMessage was received</summary>
        TransferDispatchReceived,
        /// <summary>TransferConfirmationMessage was sent</summary>
        TransferConfirmationSent,
        /// <summary>TransferConfirmationMessage was received</summary>
        TransferConfirmationReceived,
        /// <summary>TeminationMessage was sent</summary>
        TeminationSent,
        /// <summary>TeminationMessage was received</summary>
        TeminationReceived,
    }
}
