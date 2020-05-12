using System.Threading.Tasks;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient
{
    public class VaspInformationBuilder
    {
        private readonly IEthereumRpc _nodeClientEthereumRpc;

        public VaspInformationBuilder(IEthereumRpc nodeClientEthereumRpc)
        {
            _nodeClientEthereumRpc = nodeClientEthereumRpc;
        }

        public async Task<(VaspInformation VaspInformation, VaspCode VaspCode)> CreateForNaturalPersonAsync(
            string vaspSmartContractAddress,
            NaturalPersonId[] naturalPersonIds,
            PlaceOfBirth placeOfBirth)
        {
            var vaspContractInfo = await _nodeClientEthereumRpc.GetVaspContractInfoAync(vaspSmartContractAddress);
            var vaspInformation = new VaspInformation(
                vaspContractInfo.Name,
                vaspSmartContractAddress,
                vaspContractInfo.HandshakeKey,//Ensure it is correct
                vaspContractInfo.Address,
                placeOfBirth,
                naturalPersonIds,
                null,
                null);

            return (vaspInformation, vaspContractInfo.VaspCode);
        }

        public async Task<(VaspInformation VaspInformation, VaspCode VaspCode)> CreateForJuridicalPersonAsync(
            string vaspSmartContractAddress,
            JuridicalPersonId[] juridicalIds)
        {
            var vaspContractInfo = await _nodeClientEthereumRpc.GetVaspContractInfoAync(vaspSmartContractAddress);
            var vaspInformation = new VaspInformation(
                vaspContractInfo.Name,
                vaspSmartContractAddress,
                vaspContractInfo.HandshakeKey,//Ensure it is correct
                vaspContractInfo.Address,
                null,
                null,
                juridicalIds,
                null);

            return (vaspInformation, vaspContractInfo.VaspCode);
        }

        public async Task<(VaspInformation VaspInformation, VaspCode VaspCode)> CreateForBankAsync(
            string vaspSmartContractAddress,
            string settingsBic)
        {
            var vaspContractInfo = await _nodeClientEthereumRpc.GetVaspContractInfoAync(vaspSmartContractAddress);
            var vaspInformation = new VaspInformation(
                vaspContractInfo.Name,
                vaspSmartContractAddress,
                vaspContractInfo.HandshakeKey,//Ensure it is correct
                vaspContractInfo.Address,
                null,
                null,
                null,
                settingsBic);

            return (vaspInformation, vaspContractInfo.VaspCode);
        }

        public static Task<(VaspInformation VaspInformation, VaspCode VaspCode)> CreateForNaturalPersonAsync(
            IEthereumRpc ethereumRpc,
            string vaspSmartContractAddress,
            NaturalPersonId[] settingsNaturalPersonIds,
            PlaceOfBirth settingsPlaceOfBirth)
        {
            var vaspInformationBuilder = new VaspInformationBuilder(ethereumRpc);

            return vaspInformationBuilder.CreateForNaturalPersonAsync(
                vaspSmartContractAddress,
                settingsNaturalPersonIds,
                settingsPlaceOfBirth);
        }

        public static Task<(VaspInformation VaspInformation, VaspCode VaspCode)> CreateForJuridicalPersonAsync(
            IEthereumRpc ethereumRpc,
            string vaspSmartContractAddress,
            JuridicalPersonId[] juridicalIds)
        {
            var vaspInformationBuilder = new VaspInformationBuilder(ethereumRpc);

            return vaspInformationBuilder.CreateForJuridicalPersonAsync(vaspSmartContractAddress, juridicalIds);
        }

        public static Task<(VaspInformation VaspInformation, VaspCode VaspCode)> CreateForBankAsync(
            IEthereumRpc ethereumRpc,
            string vaspSmartContractAddress,
            string settingsBic)
        {
            var vaspInformationBuilder = new VaspInformationBuilder(ethereumRpc);

            return vaspInformationBuilder.CreateForBankAsync(vaspSmartContractAddress, settingsBic);
        }
    }
}