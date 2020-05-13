using OpenVASP.CSharpClient.Sessions;

namespace OpenVASP.CSharpClient.Events
{
    public class BeneficiarySessionCreatedEvent : SessionEventBase
    {
        public BeneficiarySession Session { set; get; }

        public BeneficiarySessionCreatedEvent(BeneficiarySession session)
            : base(session.Id)
        {
            Session = session;
        }
    }
}