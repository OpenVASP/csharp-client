namespace OpenVASP.CSharpClient.Events
{
    public class SessionTerminationEvent : SessionEventBase
    {
        public SessionTerminationEvent(string sessionId)
            : base(sessionId)
        {
        }
    }
}