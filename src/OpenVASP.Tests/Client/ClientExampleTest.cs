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

namespace OpenVASP.Tests.Client
{
    public class ClientExampleTest
    {
        private readonly IEnsProvider _fakeEnsProvider;
        private readonly WhisperSignService _signService;
        private readonly EthereumRpc _ethereumRpc;
        private readonly WhisperTransportClient _transportClient;
        private readonly VaspTestSettings _settings;

        public ClientExampleTest()
        {
            string whisperRpcUrl = "http://144.76.25.187:8025"; //Environment.GetEnvironmentVariable( "WHISPER_RPC_URL");
            string ethereumRpcUrl = "https://ropsten.infura.io/v3/fb49e892176d413d85f993d0352a0971"; //Environment.GetEnvironmentVariable("ETHEREUM_RPC_URL");

            _fakeEnsProvider = new FakeEnsProvider();
            _signService = new WhisperSignService();
            _ethereumRpc = new EthereumRpc(new Web3(ethereumRpcUrl));
            var whisperRpc = new WhisperRpc(new Web3(whisperRpcUrl), new WhisperMessageFormatter());
            _transportClient = new WhisperTransportClient(whisperRpc, _signService, new WhisperMessageFormatter());

            _settings = new VaspTestSettings
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
            (VaspInformation vaspInfoPerson, VaspCode vaspCodePerson) = await VaspInformationBuilder.CreateForNaturalPersonAsync(
                _ethereumRpc,
                _settings.VaspSmartContractAddressPerson,
                _settings.NaturalPersonIds,
                _settings.PlaceOfBirth);

            (VaspInformation vaspInfoJuridical, VaspCode vaspCodeJuridical) = await VaspInformationBuilder.CreateForJuridicalPersonAsync(
                _ethereumRpc,
                _settings.VaspSmartContractAddressJuridical,
                _settings.JuridicalIds);

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
            
            var sessionRequestSemaphore = new SemaphoreSlim(0, 1);
            var sessionReplySemaphore = new SemaphoreSlim(0, 1);
            var transferRequestSemaphore = new SemaphoreSlim(0, 1);
            var transferReplySemaphore = new SemaphoreSlim(0, 1);
            var transferConfirmSemaphore = new SemaphoreSlim(0, 1);
            var transferDispatchSemaphore = new SemaphoreSlim(0, 1);
            var terminationSemaphore = new SemaphoreSlim(0, 1);

            var sessionRequestReceived = false;
            var sessionReplyReceived = false;
            var transferRequestReceived = false;
            var transferReplyReceived = false;
            var transferDispatchReceived = false;
            var transferConfirmReceived = false;
            var terminationReceived = false;

            var originatorClient = VaspClient.Create(
                vaspInfoPerson,
                vaspCodePerson,
                _settings.PersonHandshakePrivateKeyHex,
                _settings.PersonSignaturePrivateKeyHex,
                _ethereumRpc,
                _fakeEnsProvider,
                _signService,
                _transportClient);
            originatorClient.SessionReplyMessageReceived += evt =>
            {
                sessionReplyReceived = true;
                sessionReplySemaphore.Release();

                return originatorClient.TransferRequestAsync(
                    evt.SessionId,
                    originatorDoc,
                    new Beneficiary("name", beneficiaryVaan.Vaan),
                    VirtualAssetType.BTC,
                    123);
            };
            originatorClient.TransferReplyMessageReceived += evt =>
            {
                transferReplyReceived = true;
                transferReplySemaphore.Release();

                return originatorClient.TransferDispatchAsync(
                    evt.SessionId,
                    "hash",
                    "sending_addr");
            };
            originatorClient.TransferConfirmationMessageReceived += evt =>
            {
                transferConfirmReceived = true;
                transferConfirmSemaphore.Release();

                return originatorClient.TerminateAsync(evt.SessionId,
                    TerminationMessage.TerminationMessageCode.SessionClosedTransferOccured);
            };

            var beneficiaryClient = VaspClient.Create(
                vaspInfoJuridical,
                vaspCodeJuridical,
                _settings.JuridicalHandshakePrivateKeyHex,
                _settings.JuridicalSignaturePrivateKeyHex,
                _ethereumRpc,
                _fakeEnsProvider,
                _signService,
                _transportClient);
            beneficiaryClient.SessionRequestMessageReceived += evt =>
            {
                sessionRequestReceived = true;
                sessionRequestSemaphore.Release();

                return beneficiaryClient.SessionReplyAsync(evt.SessionId, SessionReplyMessage.SessionReplyMessageCode.SessionAccepted);
            };
            beneficiaryClient.TransferRequestMessageReceived += evt =>
            {
                transferRequestReceived = true;
                transferRequestSemaphore.Release();

                return beneficiaryClient.TransferReplyAsync(
                    evt.SessionId,
                    TransferReplyMessage.Create(
                        evt.SessionId,
                        TransferReplyMessage.TransferReplyMessageCode.TransferAccepted));
            };
            beneficiaryClient.TransferDispatchMessageReceived += evt =>
            {
                transferDispatchReceived = true;
                transferDispatchSemaphore.Release();

                return beneficiaryClient.TransferConfirmAsync(evt.SessionId, TransferConfirmationMessage.Create(
                    evt.SessionId,
                    TransferConfirmationMessage.TransferConfirmationMessageCode.TransferConfirmed));
            };
            beneficiaryClient.TerminationMessageReceived += evt =>
            {
                terminationReceived = true;
                terminationSemaphore.Release();

                return Task.CompletedTask;
            };

            var sessionInfo = await originatorClient.CreateOriginatorSessionAsync(beneficiaryVaan.VaspCode);

            await originatorClient.SessionRequestAsync(sessionInfo.Id);
            
            await originatorClient.CloseSessionAsync(sessionInfo.Id);
            
            await originatorClient.CreateOriginatorSessionAsync(beneficiaryVaan.VaspCode, sessionInfo);
            
            await Task.WhenAny(
                Task.Delay(TimeSpan.FromMinutes(2)),
                Task.WhenAll(
                    sessionRequestSemaphore.WaitAsync(),
                    sessionReplySemaphore.WaitAsync(),
                    transferRequestSemaphore.WaitAsync(),
                    transferReplySemaphore.WaitAsync(),
                    transferDispatchSemaphore.WaitAsync(),
                    transferConfirmSemaphore.WaitAsync(),
                    terminationSemaphore.WaitAsync()));

            Assert.True(sessionRequestReceived, "Session request message was not delivered");
            Assert.True(sessionReplyReceived, "Session reply message was not delivered");
            Assert.True(transferRequestReceived, "Transfer request message was not delivered");
            Assert.True(transferReplyReceived, "Transfer reply message was not delivered");
            Assert.True(transferDispatchReceived, "Transfer dispatch message was not delivered");
            Assert.True(transferConfirmReceived, "Transfer confirm message was not delivered");
            Assert.True(terminationReceived, "Termination message was not delivered");
        }
    }
}