using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging.Messages.Entities;

namespace OpenVASP.CSharpClient
{
    public class EthereumRpc : IEthereumRpc
    {
        private readonly IWeb3 _web3;

        public EthereumRpc(IWeb3 web3)
        {
            this._web3 = web3;
        }

        [FunctionOutput]
        private class PostalAddressDTO : IFunctionOutputDTO
        {
            [Parameter("string", "streetName", 1)]
            public virtual string StreetName { get; set; }

            [Parameter("string", "buildingNumber", 2)]
            public virtual string BuildingNumber { get; set; }

            [Parameter("string", "addressLine", 3)]
            public virtual string AddressLine { get; set; }

            [Parameter("string", "postCode", 4)]
            public virtual string PostCode { get; set; }

            [Parameter("string", "town", 5)]
            public virtual string Town { get; set; }

            [Parameter("string", "country", 6)]
            public virtual string Country { get; set; }
        }

        public async Task<VaspContractInfo> GetVaspContractInfoAync(string vaspSmartContracAddress)
        {
            var pathToAbi = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VASP.abi");
            var abi = File.ReadAllText(pathToAbi);
            var contract = _web3.Eth.GetContract(abi, vaspSmartContracAddress);

            var name = await contract.GetFunction("name").CallAsync<string>();
            var vaspCode = await contract.GetFunction("code").CallAsync<byte[]>();
            var handshakeKey = await contract.GetFunction("handshakeKey").CallAsync<string>();
            var signingKey = await contract.GetFunction("signingKey").CallAsync<string>();
            var postalAddress = await contract.GetFunction("postalAddress").CallDeserializingToObjectAsync<PostalAddressDTO>();

            Country.List.TryGetValue(postalAddress.Country, out var country);

            var vaspSmartContractInfo = new VaspContractInfo()
            {
                Name = name,
                VaspCode = VaspCode.Create(vaspCode.ToHex()),
                HandshakeKey = handshakeKey,
                Address = new PostalAddress(
                    postalAddress.StreetName,
                    postalAddress.BuildingNumber,
                    postalAddress.AddressLine,
                    postalAddress.PostCode,
                    postalAddress.Town,
                    country),
                SigningKey = signingKey,
                Channgels = null,
                Email = null,
                OwnerAddress = null,
                Website = null
            };

            return vaspSmartContractInfo;
        }
    }
}