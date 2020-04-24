using System.Runtime.Serialization;

namespace OpenVASP.Messaging.Messages
{
    public enum MessageType
    {
        [EnumMember(Value = "110")]
        SessionRequest = 110,

        [EnumMember(Value = "150")]
        SessionReply = 150,

        [EnumMember(Value = "210")]
        TransferRequest = 210,

        [EnumMember(Value = "250")]
        TransferReply = 250,

        [EnumMember(Value = "310")]
        TransferDispatch = 310,

        [EnumMember(Value = "350")]
        TransferConfirmation = 350,

        [EnumMember(Value = "910")]
        Termination = 910,
    }
}
