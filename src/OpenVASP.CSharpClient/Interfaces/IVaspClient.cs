using System.Threading.Tasks;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient.Interfaces
{
    public interface IVaspClient
    {
        VaspCode VaspCode { get; }

        Task<BeneficiarySession> CreateBeneficiarySessionAsync(BeneficiarySessionInfo sessionInfo);

        Task<OriginatorSession> CreateOriginatorSessionAsync(
            VaspCode vaspCode,
            OriginatorSessionInfo sessionInfo = null);

        Task CloseSessionAsync(string sessionId);
    }
}