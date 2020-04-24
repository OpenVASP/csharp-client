using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Web3;
using OpenVASP.CSharpClient;
using OpenVASP.CSharpClient.Cryptography;
using OpenVASP.CSharpClient.Delegates;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;
using Xunit;
using Xunit.Abstractions;
using Transaction = OpenVASP.Messaging.Messages.Entities.Transaction;

namespace OpenVASP.Tests.Client
{
    public class ClientExampleTest
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly IEnsProvider _fakeEnsProvider;
        private readonly WhisperSignService _signService;
        private readonly WhisperRpc _whisperRpc;

        public INodeClient NodeClient { get; set; }

        public VaspTestSettings Settings { get; set; }

        public ClientExampleTest(ITestOutputHelper testOutputHelper)
        {
            var all = Environment.GetEnvironmentVariables();
            string whisperRpcUrl = "http://144.76.25.187:8025"; //Environment.GetEnvironmentVariable( "WHISPER_RPC_URL");
            string ethereumRpcUrl = "https://ropsten.infura.io/v3/fb49e892176d413d85f993d0352a0971"; //Environment.GetEnvironmentVariable("ETHEREUM_RPC_URL");

            this._fakeEnsProvider = new FakeEnsProvider();
            this._testOutputHelper = testOutputHelper;
            this._signService = new WhisperSignService();
            this._whisperRpc = new WhisperRpc(new Web3(whisperRpcUrl), new WhisperMessageFormatter());

            var ethRpc = new EthereumRpc(new Web3(ethereumRpcUrl));
            
            NodeClient = new NodeClient()
            {
                EthereumRpc = ethRpc,
                WhisperRpc =_whisperRpc,
                TransportClient = new TransportClient(_whisperRpc, _signService, new WhisperMessageFormatter())
            };
            Settings = new VaspTestSettings()
            {
                PersonHandshakePrivateKeyHex =    "0xe7578145d518e5272d660ccfdeceedf2d55b90867f2b7a6e54dc726662aebac2",
                PersonSignaturePrivateKeyHex = "0x790a3437381e0ca44a71123d56dc64a6209542ddd58e5a56ecdb13134e86f7c6",
                VaspSmartContractAddressPerson = "0x6befaf0656b953b188a0ee3bf3db03d07dface61",
                VaspSmartContractAddressJuridical = "0x08FDa931D64b17c3aCFfb35C1B3902e0BBB4eE5C",
                JuridicalSignaturePrivateKeyHex = "0x6854a4e4f8945d9fa215646a820fe9a866b5635ffc7cfdac29711541f7b913f9",
                JuridicalHandshakePrivateKeyHex = "0x502eb0b1a40d5b788b2395394bc6ae47adae61e9f0a9584c4700132914a8ed04",
                PlaceOfBirth = new PlaceOfBirth(DateTime.UtcNow, "Town X", Country.List["DE"]),
                NaturalPersonIds = new NaturalPersonId[]
                {
                    new NaturalPersonId("ID", NaturalIdentificationType.PassportNumber, Country.List["DE"]), 
                },
                JuridicalIds = new JuridicalPersonId[]
                {
                    new JuridicalPersonId("ID", JuridicalIdentificationType.BankPartyIdentification, Country.List["DE"]),
                },
            };
        }
    }
}