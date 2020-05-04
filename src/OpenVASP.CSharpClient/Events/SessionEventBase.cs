namespace OpenVASP.CSharpClient.Events
{
    public abstract class SessionEventBase
    {
        public string SessionId { get; }

        public SessionEventBase(string sessionId)
        {
            this.SessionId = sessionId;
        }
    }
}
