using System;
using System.Linq;
using System.Threading;
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
        private readonly EthereumRpc _ethereumRpc;
        private readonly WhisperTransportClient _transportClient;

        public VaspTestSettings Settings { get; set; }

        public ClientExampleTest(ITestOutputHelper testOutputHelper)
        {
            var all = Environment.GetEnvironmentVariables();
            string
                whisperRpcUrl = "http://144.76.25.187:8025"; //Environment.GetEnvironmentVariable( "WHISPER_RPC_URL");
            string ethereumRpcUrl =
                "https://ropsten.infura.io/v3/fb49e892176d413d85f993d0352a0971"; //Environment.GetEnvironmentVariable("ETHEREUM_RPC_URL");

            this._fakeEnsProvider = new FakeEnsProvider();
            this._testOutputHelper = testOutputHelper;
            this._signService = new WhisperSignService();
            this._ethereumRpc = new EthereumRpc(new Web3(ethereumRpcUrl));
            this._whisperRpc = new WhisperRpc(new Web3(whisperRpcUrl), new WhisperMessageFormatter());
            this._transportClient = new WhisperTransportClient(_whisperRpc, _signService, new WhisperMessageFormatter());

            Settings = new VaspTestSettings()
            {
                PersonHandshakePrivateKeyHex = "0xe7578145d518e5272d660ccfdeceedf2d55b90867f2b7a6e54dc726662aebac2",
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
                    new JuridicalPersonId("ID", JuridicalIdentificationType.BankPartyIdentification,
                        Country.List["DE"]),
                },
            };
        }

        [Fact]
        public async Task ConnectionBetweenTwoClientsSuccessful()
        {
            (VaspInformation vaspInfoPerson, VaspContractInfo vaspContractInfoPerson) = await VaspInformationBuilder.CreateForNaturalPersonAsync(
                _ethereumRpc,
                Settings.VaspSmartContractAddressPerson,
                Settings.NaturalPersonIds,
                Settings.PlaceOfBirth);

            (VaspInformation vaspInfoJuridical, VaspContractInfo vaspContractInfoJuridical) = await VaspInformationBuilder.CreateForJuridicalPersonAsync(
                _ethereumRpc,
                Settings.VaspSmartContractAddressJuridical,
                Settings.JuridicalIds);
            
            var originatorVaan =  VirtualAssetsAccountNumber.Create(   vaspInfoPerson.GetVaspCode(), "524ee3fb082809");
            var beneficiaryVaan = VirtualAssetsAccountNumber.Create(vaspInfoJuridical.GetVaspCode(), "524ee3fb082809");
            
            var originatorDoc = Originator.CreateOriginatorForNaturalPerson(
                "Test van der Test",
                originatorVaan,
                new PostalAddress(
                    "StreetX",
                    44,
                    "AddressLineX",
                    "510051",
                    "TownX",
                    Country.List["DE"]),
                new PlaceOfBirth(DateTime.Today.AddYears(-30), "TownX", Country.List["DE"]),
                new NaturalPersonId[]
                {
                    new NaturalPersonId("Id", NaturalIdentificationType.NationalIdentityNumber, Country.List["DE"]), 
                });
            
            TestVaspCallbacks handler = new TestVaspCallbacks(null, null, null, null, null, null);

            var originator = VaspClient.Create(
                vaspInfoPerson,
                vaspContractInfoPerson.VaspCode,
                Settings.PersonHandshakePrivateKeyHex,
                Settings.PersonSignaturePrivateKeyHex,
                _ethereumRpc,
                _fakeEnsProvider,
                _signService,
                _transportClient,
                handler);
            
            originator.Dispose();

            var beneficiary = VaspClient.Create(
                vaspInfoJuridical,
                vaspContractInfoJuridical.VaspCode,
                Settings.JuridicalHandshakePrivateKeyHex,
                Settings.JuridicalSignaturePrivateKeyHex,
                _ethereumRpc,
                _fakeEnsProvider,
                _signService,
                _transportClient,
                handler);
            
            beneficiary.Dispose();
            
            var sessionRequestSemaphore = new SemaphoreSlim(0, 1);
            var sessionReplySemaphore = new SemaphoreSlim(0, 1);
            var transferRequestSemaphore = new SemaphoreSlim(0, 1);
            var transferReplySemaphore = new SemaphoreSlim(0, 1);
            var transferConfirmSemaphore = new SemaphoreSlim(0, 1);
            var transferDispatchSemaphore = new SemaphoreSlim(0, 1);

            var sessionRequestReceived = false;
            var sessionReplyReceived = false;
            var transferRequestReceived = false;
            var transferReplyReceived = false;
            var transferDispatchReceived = false;
            var transferConfirmReceived = false;
            
            handler = new TestVaspCallbacks(
                (s, message) =>
                {
                    sessionRequestReceived = true;
                    sessionRequestSemaphore.Release();
                    
                    return beneficiary.SessionReplyAsync(s, SessionReplyMessage.SessionReplyMessageCode.SessionAccepted);
                },
                (s, message) =>
                {
                    sessionReplyReceived = true;
                    sessionReplySemaphore.Release();
                    
                    return originator.TransferRequestAsync(s, "name", VirtualAssetType.BTC, 123);
                }, (s, message) =>
                {
                    transferRequestReceived = true;
                    transferRequestSemaphore.Release();
                    
                    return beneficiary.TransferReplyAsync(
                        s,
                        TransferReplyMessage.Create(
                            s,
                            TransferReplyMessage.TransferReplyMessageCode.TransferAccepted,
                            originatorDoc,
                            new Beneficiary("name", "vaan"),
                            new TransferReply(VirtualAssetType.BTC, TransferType.BlockchainTransfer, 123, "dest"),
                            vaspInfoJuridical));
                }, (s, message) =>
                {
                    transferReplyReceived = true;
                    transferReplySemaphore.Release();
                    
                    return originator.TransferDispatchAsync(
                        s,
                        new TransferReply(VirtualAssetType.BTC, TransferType.BlockchainTransfer, 123, "dest"),
                        "hash",
                        "sending_addr",
                        "benef_name");
                }, (s, message) =>
                {
                    transferConfirmReceived = true;
                    transferConfirmSemaphore.Release();
                    
                    return Task.CompletedTask;
                }, (s, message) =>
                {
                    transferDispatchReceived = true;
                    transferDispatchSemaphore.Release();
                    
                    return beneficiary.TransferConfirmAsync(s, TransferConfirmationMessage.Create(
                        s,
                        TransferConfirmationMessage.TransferConfirmationMessageCode.TransferConfirmed,
                        originatorDoc,
                        new Beneficiary("name", "vaan"),
                        new TransferReply(VirtualAssetType.BTC, TransferType.BlockchainTransfer, 123, "dest"),
                        new Transaction("txid", DateTime.UtcNow, "sendingaddr"),
                        vaspInfoJuridical));
                });
            
            originator = VaspClient.Create(
                vaspInfoPerson,
                vaspContractInfoPerson.VaspCode,
                Settings.PersonHandshakePrivateKeyHex,
                Settings.PersonSignaturePrivateKeyHex,
                _ethereumRpc,
                _fakeEnsProvider,
                _signService,
                _transportClient,
                handler);

            beneficiary = VaspClient.Create(
                vaspInfoJuridical,
                vaspContractInfoJuridical.VaspCode,
                Settings.JuridicalHandshakePrivateKeyHex,
                Settings.JuridicalSignaturePrivateKeyHex,
                _ethereumRpc,
                _fakeEnsProvider,
                _signService,
                _transportClient,
                handler);

            await originator.CreateSessionAsync(originatorDoc, beneficiaryVaan);

            await Task.WhenAny(
                Task.Delay(TimeSpan.FromMinutes(1)),
                Task.WhenAll(
                    sessionRequestSemaphore.WaitAsync(),
                    sessionReplySemaphore.WaitAsync(),
                    transferRequestSemaphore.WaitAsync(),
                    transferReplySemaphore.WaitAsync(),
                    transferDispatchSemaphore.WaitAsync(),
                    transferConfirmSemaphore.WaitAsync()));
            
            Assert.True(sessionRequestReceived);
            Assert.True(sessionReplyReceived);
            Assert.True(transferRequestReceived);
            Assert.True(transferReplyReceived);
            Assert.True(transferDispatchReceived);
            Assert.True(transferConfirmReceived);
        }
    }

    public class TestVaspCallbacks : IVaspCallbacks
    {
        private Func<string, SessionRequestMessage, Task> _sessionRequestCallback;
        private Func<string, SessionReplyMessage, Task> _sessionReplyCallback;
        private Func<string, TransferRequestMessage, Task> _transferRequestCallback;
        private Func<string, TransferReplyMessage, Task> _transferReplyCallback;
        private Func<string, TransferConfirmationMessage, Task> _transferConfirmCallback;
        private Func<string, TransferDispatchMessage, Task> _transferDispatchCallback;

        public TestVaspCallbacks(
            Func<string, SessionRequestMessage, Task> sessionRequestCallback,
            Func<string, SessionReplyMessage, Task> sessionReplyCallback,
            Func<string, TransferRequestMessage, Task> transferRequestCallback,
            Func<string, TransferReplyMessage, Task> transferReplyCallback,
            Func<string, TransferConfirmationMessage, Task> transferConfirmCallback,
            Func<string, TransferDispatchMessage, Task> transferDispatchCallback)
        {
            _sessionReplyCallback = sessionReplyCallback;
            _transferRequestCallback = transferRequestCallback;
            _transferReplyCallback = transferReplyCallback;
            _transferConfirmCallback = transferConfirmCallback;
            _transferDispatchCallback = transferDispatchCallback;
            _sessionRequestCallback = sessionRequestCallback;
        }

        public Task SessionRequestMessageReceivedAsync(string sessionId, SessionRequestMessage message)
        {
            return _sessionRequestCallback.Invoke(sessionId, message);
        }

        public Task SessionReplyMessageReceivedAsync(string sessionId, SessionReplyMessage message)
        {
            return _sessionReplyCallback.Invoke(sessionId, message);
        }

        public Task TransferReplyMessageReceivedAsync(string sessionId, TransferReplyMessage message)
        {
            return _transferReplyCallback.Invoke(sessionId, message);
        }

        public Task TransferConfirmationMessageReceivedAsync(string sessionId, TransferConfirmationMessage message)
        {
            return _transferConfirmCallback.Invoke(sessionId, message);
        }

        public Task TransferRequestMessageReceivedAsync(string sessionId, TransferRequestMessage message)
        {
            return _transferRequestCallback.Invoke(sessionId, message);
        }

        public Task TransferDispatchMessageReceivedAsync(string sessionId, TransferDispatchMessage message)
        {
            return _transferDispatchCallback.Invoke(sessionId, message);
        }
    }
}