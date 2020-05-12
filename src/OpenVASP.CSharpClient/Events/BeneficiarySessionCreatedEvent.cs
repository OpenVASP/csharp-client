using OpenVASP.CSharpClient.Sessions;

namespace OpenVASP.CSharpClient.Events
{
    public class BeneficiarySessionCreatedEvent : SessionEventBase
    {
        public BeneficiarySessionInfo SessionInfo { set; get; }
        
        public BeneficiarySessionCreatedEvent(string sessionId, BeneficiarySessionInfo sessionInfo)
            : base(sessionId)
        {
            SessionInfo = sessionInfo;
        }
    }
}