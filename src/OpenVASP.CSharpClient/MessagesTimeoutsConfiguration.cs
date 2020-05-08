using System;

namespace OpenVASP.CSharpClient
{
    /// <summary>
    /// Messages timeouts configuration.
    /// </summary>
    public class MessagesTimeoutsConfiguration
    {
        private TimeSpan? _sessionRequestMessageTimeout;
        private int? _sessionRequestMessageMaxRetriesCount;
        private TimeSpan? _sessionReplyMessageTimeout;
        private int? _sessionReplyMessageMaxRetriesCount;
        private TimeSpan? _transferReplyMessageTimeout;
        private int? _transferReplyMessageMaxRetriesCount;
        private TimeSpan? _transferConfirmationMessageTimeout;
        private int? _transferConfirmationMessageMaxRetriesCount;
        private TimeSpan? _transferRequestMessageTimeout;
        private int? _transferRequestMessageMaxRetriesCount;
        private TimeSpan? _transferDispatchMessageTimeout;
        private int? _transferDispatchMessageMaxRetriesCount;

        /// <summary>Message timeout applied by default</summary>
        public readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

        /// <summary>Max retries count applied by default</summary>
        public readonly int DefaultMaxRetriesCount = 0;

        /// <summary>Session request message timeout</summary>
        public TimeSpan? SessionRequestMessageTimeout
        {
            get => _sessionRequestMessageTimeout ?? DefaultTimeout;
            set => _sessionRequestMessageTimeout = value;
        }

        /// <summary>Session request message max retries count</summary>
        public int? SessionRequestMessageMaxRetriesCount
        {
            get => _sessionRequestMessageMaxRetriesCount ?? DefaultMaxRetriesCount;
            set => _sessionRequestMessageMaxRetriesCount = value;
        }

        /// <summary>Session reply message timeout</summary>
        public TimeSpan? SessionReplyMessageTimeout
        {
            get => _sessionReplyMessageTimeout ?? DefaultTimeout;
            set => _sessionReplyMessageTimeout = value;
        }

        /// <summary>Session reply message max retries count</summary>
        public int? SessionReplyMessageMaxRetriesCount
        {
            get => _sessionReplyMessageMaxRetriesCount ?? DefaultMaxRetriesCount;
            set => _sessionReplyMessageMaxRetriesCount = value;
        }

        /// <summary>Transfer reply message timeout</summary>
        public TimeSpan? TransferReplyMessageTimeout
        {
            get => _transferReplyMessageTimeout ?? DefaultTimeout;
            set => _transferReplyMessageTimeout = value;
        }

        /// <summary>Transfer reply message max retries count</summary>
        public int? TransferReplyMessageMaxRetriesCount
        {
            get => _transferReplyMessageMaxRetriesCount ?? DefaultMaxRetriesCount;
            set => _transferReplyMessageMaxRetriesCount = value;
        }

        /// <summary>Transfer confirmation message timeout</summary>
        public TimeSpan? TransferConfirmationMessageTimeout
        {
            get => _transferConfirmationMessageTimeout ?? DefaultTimeout;
            set => _transferConfirmationMessageTimeout = value;
        }

        /// <summary>Transfer confirmation message max retries count</summary>
        public int? TransferConfirmationMessageMaxRetriesCount
        {
            get => _transferConfirmationMessageMaxRetriesCount ?? DefaultMaxRetriesCount;
            set => _transferConfirmationMessageMaxRetriesCount = value;
        }

        /// <summary>Transfer request message timeout</summary>
        public TimeSpan? TransferRequestMessageTimeout
        {
            get => _transferRequestMessageTimeout ?? DefaultTimeout;
            set => _transferRequestMessageTimeout = value;
        }

        /// <summary>Transfer request message max retries count</summary>
        public int? TransferRequestMessageMaxRetriesCount
        {
            get => _transferRequestMessageMaxRetriesCount ?? DefaultMaxRetriesCount;
            set => _transferRequestMessageMaxRetriesCount = value;
        }

        /// <summary>Transfer dispatch message timeout</summary>
        public TimeSpan? TransferDispatchMessageTimeout
        {
            get => _transferDispatchMessageTimeout ?? DefaultTimeout;
            set => _transferDispatchMessageTimeout = value;
        }

        /// <summary>Transfer dispatch message max retries count</summary>
        public int? TransferDispatchMessageMaxRetriesCount
        {
            get => _transferDispatchMessageMaxRetriesCount ?? DefaultMaxRetriesCount;
            set => _transferDispatchMessageMaxRetriesCount = value;
        }
    }
}
