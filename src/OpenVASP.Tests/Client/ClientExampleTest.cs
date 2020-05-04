using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Web3;
using OpenVASP.CSharpClient;
using OpenVASP.CSharpClient.Interfaces;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;
using Xunit;
using Transaction = OpenVASP.Messaging.Messages.Entities.Transaction;

namespace OpenVASP.Tests.Client
{
    public class ClientExampleTest
    {
        private readonly IEnsProvider _fakeEnsProvider;
        private readonly WhisperSignService _signService;
        private readonly WhisperRpc _whisperRpc;
        private readonly EthereumRpc _ethereumRpc;
        private readonly WhisperTransportClient _transportClient;

        public VaspTestSettings Settings { get; set; }

        public ClientExampleTest()
        {
            string whisperRpcUrl = "http://144.76.25.187:8025"; //Environment.GetEnvironmentVariable( "WHISPER_RPC_URL");
            string ethereumRpcUrl = "https://ropsten.infura.io/v3/fb49e892176d413d85f993d0352a0971"; //Environment.GetEnvironmentVariable("ETHEREUM_RPC_URL");

            this._fakeEnsProvider = new FakeEnsProvider();
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
                NaturalPersonIds = new[]
                {
                    new NaturalPersonId("ID", NaturalIdentificationType.PassportNumber, Country.List["DE"]),
                },
                JuridicalIds = new[]
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
                new[]
                {
                    new NaturalPersonId("Id", NaturalIdentificationType.NationalIdentityNumber, Country.List["DE"]), 
                });

            var originator = VaspClient.Create(
                vaspInfoPerson,
                vaspContractInfoPerson.VaspCode,
                Settings.PersonHandshakePrivateKeyHex,
                Settings.PersonSignaturePrivateKeyHex,
                _ethereumRpc,
                _fakeEnsProvider,
                _signService,
                _transportClient);

            originator.Dispose();

            var beneficiary = VaspClient.Create(
                vaspInfoJuridical,
                vaspContractInfoJuridical.VaspCode,
                Settings.JuridicalHandshakePrivateKeyHex,
                Settings.JuridicalSignaturePrivateKeyHex,
                _ethereumRpc,
                _fakeEnsProvider,
                _signService,
                _transportClient);

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

            originator = VaspClient.Create(
                vaspInfoPerson,
                vaspContractInfoPerson.VaspCode,
                Settings.PersonHandshakePrivateKeyHex,
                Settings.PersonSignaturePrivateKeyHex,
                _ethereumRpc,
                _fakeEnsProvider,
                _signService,
                _transportClient);
            originator.SessionReplyMessageReceived += evt =>
            {
                sessionReplyReceived = true;
                sessionReplySemaphore.Release();

                return originator.TransferRequestAsync(evt.SessionId, "name", VirtualAssetType.BTC, 123);
            };
            originator.TransferReplyMessageReceived += evt =>
            {
                transferReplyReceived = true;
                transferReplySemaphore.Release();

                return originator.TransferDispatchAsync(
                    evt.SessionId,
                    new TransferReply(VirtualAssetType.BTC, TransferType.BlockchainTransfer, 123, "dest"),
                    "hash",
                    "sending_addr",
                    "benef_name");
            };
            originator.TransferConfirmationMessageReceived += evt =>
            {
                transferConfirmReceived = true;
                transferConfirmSemaphore.Release();

                return Task.CompletedTask;
            };

            beneficiary = VaspClient.Create(
                vaspInfoJuridical,
                vaspContractInfoJuridical.VaspCode,
                Settings.JuridicalHandshakePrivateKeyHex,
                Settings.JuridicalSignaturePrivateKeyHex,
                _ethereumRpc,
                _fakeEnsProvider,
                _signService,
                _transportClient);
            beneficiary.SessionRequestMessageReceived += evt =>
            {
                sessionRequestReceived = true;
                sessionRequestSemaphore.Release();

                return beneficiary.SessionReplyAsync(evt.SessionId, SessionReplyMessage.SessionReplyMessageCode.SessionAccepted);
            };
            beneficiary.TransferRequestMessageReceived += evt =>
            {
                transferRequestReceived = true;
                transferRequestSemaphore.Release();

                return beneficiary.TransferReplyAsync(
                    evt.SessionId,
                    TransferReplyMessage.Create(
                        evt.SessionId,
                        TransferReplyMessage.TransferReplyMessageCode.TransferAccepted,
                        originatorDoc,
                        new Beneficiary("name", "vaan"),
                        new TransferReply(VirtualAssetType.BTC, TransferType.BlockchainTransfer, 123, "dest"),
                        vaspInfoJuridical));
            };
            beneficiary.TransferDispatchMessageReceived += evt =>
            {
                transferDispatchReceived = true;
                transferDispatchSemaphore.Release();

                return beneficiary.TransferConfirmAsync(evt.SessionId, TransferConfirmationMessage.Create(
                    evt.SessionId,
                    TransferConfirmationMessage.TransferConfirmationMessageCode.TransferConfirmed,
                    originatorDoc,
                    new Beneficiary("name", "vaan"),
                    new TransferReply(VirtualAssetType.BTC, TransferType.BlockchainTransfer, 123, "dest"),
                    new Transaction("txid", DateTime.UtcNow, "sendingaddr"),
                    vaspInfoJuridical));
            };

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
}