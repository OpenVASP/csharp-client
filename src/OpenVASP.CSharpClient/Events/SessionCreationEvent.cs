namespace OpenVASP.CSharpClient.Events
{
    public class SessionCreatedEvent : SessionEventBase
    {
        public SessionCreatedEvent(string sessionId)
            : base(sessionId)
        {
        }
    }
}