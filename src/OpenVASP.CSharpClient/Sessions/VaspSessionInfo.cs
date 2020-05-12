namespace OpenVASP.CSharpClient.Sessions
{
    public abstract class VaspSessionInfo
    {
        public string Id { set; get; }
        public string PrivateSigningKey { get; set; }
        public string SharedEncryptionKey { set; get; }
        public string CounterPartyPublicSigningKey { set; get; }
        public string Topic { get; set; }
        public string CounterPartyTopic { get; set; }
        public string MessageFilter { get; set; }
        public string SymKey { get; set; }
        public abstract SessionType Type { get; }
    }

    public class OriginatorSessionInfo : VaspSessionInfo
    {
        public string PublicHandshakeKey { set; get; }
        public string PublicEncryptionKey { set; get; }
        public override SessionType Type => SessionType.Originator;
    }
    
    public class BeneficiarySessionInfo : VaspSessionInfo
    {
        public override SessionType Type => SessionType.Beneficiary;
    }

    public enum SessionType
    {
        Beneficiary,
        Originator
    }
}