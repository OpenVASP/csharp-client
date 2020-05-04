namespace OpenVASP.CSharpClient.Events
{
    public class SessionMessageEvent<T> : SessionEventBase
    {
        public T Message { get; }

        public SessionMessageEvent(string sessionId, T message)
            : base(sessionId)
        {
            Message = message;
        }
    }
}
