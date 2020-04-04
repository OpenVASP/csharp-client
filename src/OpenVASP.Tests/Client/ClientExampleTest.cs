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
using OpenVASP.CSharpClient.Persistence;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;
using OpenVASP.ProtoMappers;
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
            string whisperRpcUrl = Environment.GetEnvironmentVariable( "WHISPER_RPC_URL");
            string ethereumRpcUrl = Environment.GetEnvironmentVariable("ETHEREUM_RPC_URL");

            this._fakeEnsProvider = new FakeEnsProvider();
            this._testOutputHelper = testOutputHelper;
            this._signService = new WhisperSignService();
            this._whisperRpc = new WhisperRpc(new Web3(whisperRpcUrl), new WhisperMessageFormatter());

            NodeClient = new NodeClient()
            {
                EthereumRpc = new EthereumRpc(new Web3(ethereumRpcUrl)),
                WhisperRpc =_whisperRpc,
                TransportClient = new TransportClient(_whisperRpc, _signService, new WhisperMessageFormatter())
            };
            Settings = new VaspTestSettings()
            {
                PersonHandshakePrivateKeyHex =    "0xe7578145d518e5272d660ccfdeceedf2d55b90867f2b7a6e54dc726662aebac2",
                PersonSignaturePrivateKeyHex = "0x790a3437381e0ca44a71123d56dc64a6209542ddd58e5a56ecdb13134e86f7c6",
                VaspSmartContractAddressPerson = "0x6befaf0656b953b188a0ee3bf3db03d07dface61",
                VaspSmartContractAddressJuridical = "0x08FDa931D64b17c3aCFfb35C1B3902e0BBB4eE5C",
                VaspSmartContractAddressBank = "0x4Dd7E1E2d5640a06ed81f155f171012F1CD48DAA",
                JuridicalSignaturePrivateKeyHex = "0x6854a4e4f8945d9fa215646a820fe9a866b5635ffc7cfdac29711541f7b913f9",
                JuridicalHandshakePrivateKeyHex = "0x502eb0b1a40d5b788b2395394bc6ae47adae61e9f0a9584c4700132914a8ed04",
                BankHandshakePrivateKeyHex = "0x909aa47d973d34adf9bc44d6e1d755f89b9ca2972da77966336972a602a243db",
                BankSignaturePrivateKeyHex = "0x9b3aaa8e802a1d42cf112dfc0e035440c451102d358163d345f6b3968415b077",
                PlaceOfBirth = new PlaceOfBirth(DateTime.UtcNow, "Town X", Country.List["DE"]),
                NaturalPersonIds = new NaturalPersonId[]
                {
                    new NaturalPersonId("ID", NaturalIdentificationType.PassportNumber, Country.List["DE"]), 
                },
                Bic = "AUZDDEM1XXX",
                JuridicalIds = new JuridicalPersonId[]
                {
                    new JuridicalPersonId("ID", JuridicalIdentificationType.BankPartyIdentification, Country.List["DE"]),
                },
            };
        }

        [Fact]
        public async Task CreateVaspForNaturalPerson_Builder()
        {
            var builder = new VaspInformationBuilder(NodeClient.EthereumRpc);
            var messageRepository = new InMemoryMessageRepository();

            (VaspInformation vaspInfo, VaspContractInfo vaspContractInfo) = await builder.CreateForNaturalPersonAsync(
                Settings.VaspSmartContractAddressPerson,
                Settings.NaturalPersonIds,
                Settings.PlaceOfBirth);

            VaspClient vasp = VaspClient.Create(
                vaspInfo,
                vaspContractInfo,
                Settings.PersonHandshakePrivateKeyHex,
                Settings.PersonSignaturePrivateKeyHex,
                NodeClient.EthereumRpc,
                NodeClient.WhisperRpc,
                _fakeEnsProvider,
                _signService,
                NodeClient.TransportClient,
                messageRepository);

            // VASP paramaters must be derived from smart contract
            Assert.NotNull(vasp.VaspInfo.Name);
            Assert.NotNull(vasp.VaspInfo.VaspIdentity);
            Assert.NotNull(vasp.VaspInfo.PostalAddress);

            // VASP natural person parameters should be same what we pass in constructor
            Assert.True(vasp.VaspInfo.NaturalPersonIds.SequenceEqual(Settings.NaturalPersonIds));
            Assert.Equal(vasp.VaspInfo.PlaceOfBirth, Settings.PlaceOfBirth);

            Assert.Null(vasp.VaspInfo.JuridicalPersonIds);
            Assert.Null(vasp.VaspInfo.BIC);
        }

        [Fact]
        public async Task CreateVaspForNaturalPerson_Static()
        {
            var messageRepository = new InMemoryMessageRepository();
            (VaspInformation vaspInfo, VaspContractInfo vaspContractInfo) = await VaspInformationBuilder.CreateForNaturalPersonAsync(
                NodeClient.EthereumRpc,
                Settings.VaspSmartContractAddressPerson,
                Settings.NaturalPersonIds,
                Settings.PlaceOfBirth);

            VaspClient vasp = VaspClient.Create(
                vaspInfo,
                vaspContractInfo,
                Settings.PersonHandshakePrivateKeyHex,
                Settings.PersonSignaturePrivateKeyHex,
                NodeClient.EthereumRpc,
                NodeClient.WhisperRpc,
                _fakeEnsProvider,
                _signService,
                NodeClient.TransportClient,
                messageRepository);

            // VASP paramaters must be derived from smart contract
            Assert.NotNull(vasp.VaspInfo.Name);
            Assert.NotNull(vasp.VaspInfo.VaspIdentity);
            Assert.NotNull(vasp.VaspInfo.PostalAddress);

            // VASP natural person parameters should be same what we pass in constructor
            Assert.True(vasp.VaspInfo.NaturalPersonIds.SequenceEqual(Settings.NaturalPersonIds));
            Assert.Equal(vasp.VaspInfo.PlaceOfBirth, Settings.PlaceOfBirth);

            Assert.Null(vasp.VaspInfo.JuridicalPersonIds);
            Assert.Null(vasp.VaspInfo.BIC);
        }

        [Fact]
        public async Task CreateVaspForJuridicalPerso()
        {
            var messageRepository = new InMemoryMessageRepository();
            (VaspInformation vaspInfo, VaspContractInfo vaspContractInfo) = await VaspInformationBuilder.CreateForJuridicalPersonAsync(
                NodeClient.EthereumRpc,
                Settings.VaspSmartContractAddressJuridical,
                Settings.JuridicalIds);

            VaspClient vasp = VaspClient.Create(
                vaspInfo,
                vaspContractInfo,
                Settings.JuridicalHandshakePrivateKeyHex,
                Settings.JuridicalSignaturePrivateKeyHex,
                NodeClient.EthereumRpc,
                NodeClient.WhisperRpc,
                _fakeEnsProvider,
                _signService,
                NodeClient.TransportClient,
                messageRepository);

            // VASP paramaters must be derived from smart contract
            Assert.NotNull(vasp.VaspInfo.Name);
            Assert.NotNull(vasp.VaspInfo.VaspIdentity);
            Assert.NotNull(vasp.VaspInfo.PostalAddress);

            // VASP natural person parameters should be same what we pass in constructor
            Assert.True(vasp.VaspInfo.JuridicalPersonIds.SequenceEqual(Settings.JuridicalIds));

            Assert.Null(vasp.VaspInfo.NaturalPersonIds);
            Assert.Null(vasp.VaspInfo.PlaceOfBirth);
            Assert.Null(vasp.VaspInfo.BIC);
        }

        [Fact]
        public async Task CreateVaspForBank()
        {
            var signature = new EthECKey(Settings.PersonSignaturePrivateKeyHex);
            var handshake = ECDH_Key.ImportKey(Settings.PersonHandshakePrivateKeyHex);

            var signPub = signature.GetPubKey().ToHex(prefix: true);
            var handshakePub = handshake.PublicKey;

            var messageRepository = new InMemoryMessageRepository();
            (VaspInformation vaspInfo, VaspContractInfo vaspContractInfo) = await VaspInformationBuilder.CreateForBankAsync(
                NodeClient.EthereumRpc,
                Settings.VaspSmartContractAddressBank,
                Settings.Bic);

            VaspClient vasp = VaspClient.Create(
                vaspInfo,
                vaspContractInfo,
                Settings.BankHandshakePrivateKeyHex,
                Settings.BankSignaturePrivateKeyHex,
                NodeClient.EthereumRpc,
                NodeClient.WhisperRpc,
                _fakeEnsProvider,
                _signService,
                NodeClient.TransportClient,
                messageRepository);

            // VASP paramaters must be derived from smart contract
            Assert.NotNull(vasp.VaspInfo.Name);
            Assert.NotNull(vasp.VaspInfo.VaspIdentity);
            Assert.NotNull(vasp.VaspInfo.PostalAddress);

            // VASP natural person parameters should be same what we pass in constructor
            Assert.Equal(vasp.VaspInfo.BIC, Settings.Bic);

            Assert.Null(vasp.VaspInfo.NaturalPersonIds);
            Assert.Null(vasp.VaspInfo.PlaceOfBirth);
            Assert.Null(vasp.VaspInfo.JuridicalPersonIds);
        }

        [Fact]
        public async Task CreateSessionBetweenVASPs()
        {
            int sessionTerminationCounter = 0;
            SessionTermination sessionTerminationDelegate = @event => { sessionTerminationCounter++; };
            var originatorMessageRepository = new InMemoryMessageRepository();
            var beneficiaryMessageRepository = new InMemoryMessageRepository();

            (VaspInformation vaspInfoPerson, VaspContractInfo vaspContractInfoPerson) = await VaspInformationBuilder.CreateForNaturalPersonAsync(
                NodeClient.EthereumRpc,
                Settings.VaspSmartContractAddressPerson,
                Settings.NaturalPersonIds,
                Settings.PlaceOfBirth);

            VaspClient originator = VaspClient.Create(
                vaspInfoPerson,
                vaspContractInfoPerson,
                Settings.PersonHandshakePrivateKeyHex,
                Settings.PersonSignaturePrivateKeyHex,
                NodeClient.EthereumRpc,
                NodeClient.WhisperRpc,
                _fakeEnsProvider,
                _signService,
                NodeClient.TransportClient,
                originatorMessageRepository);

            (VaspInformation vaspInfoJuridical, VaspContractInfo vaspContractInfoJuridical) = await VaspInformationBuilder.CreateForJuridicalPersonAsync(
                NodeClient.EthereumRpc,
                Settings.VaspSmartContractAddressJuridical,
                Settings.JuridicalIds);

            VaspClient beneficiary = VaspClient.Create(
                vaspInfoJuridical,
                vaspContractInfoJuridical,
                Settings.JuridicalHandshakePrivateKeyHex,
                Settings.JuridicalSignaturePrivateKeyHex,
                NodeClient.EthereumRpc,
                NodeClient.WhisperRpc,
                _fakeEnsProvider,
                _signService,
                NodeClient.TransportClient,
                beneficiaryMessageRepository);

            var originatorVaan =  VirtualAssetssAccountNumber.Create(   vaspInfoPerson.GetVaspCode(), "524ee3fb082809");
            var beneficiaryVaan = VirtualAssetssAccountNumber.Create(vaspInfoJuridical.GetVaspCode(), "524ee3fb082809");

            IVaspMessageHandler messageHandler = new VaspMessageHandlerCallbacks(
                (vaspInfo) =>
                {
                    return Task.FromResult(true);
                },
                (request, currentSession) =>
                {
                    var message = new TransferReplyMessage(currentSession.SessionId, TransferReplyMessage.TransferReplyMessageCode.TransferAccepted,
                        request.Originator, 
                        new Beneficiary("Mr. Test",request.Beneficiary.VAAN), 
                        new TransferReply(
                            request.Transfer.VirtualAssetType, 
                            request.Transfer.TransferType, 
                            request.Transfer.Amount, 
                            "0x0"),
                        request.VASP);

                    return Task.FromResult(message);
                },
                (dispatch, currentSession) =>
                {
                    var message = new TransferConfirmationMessage(currentSession.SessionId, 
                        TransferConfirmationMessage.TransferConfirmationMessageCode.TransferConfirmed,
                        dispatch.Originator,
                        dispatch.Beneficiary,
                        dispatch.Transfer,
                        dispatch.Transaction,
                        dispatch.VASP);

                    return Task.FromResult(message);
                });

            beneficiary.RunListener(messageHandler);

            originator.SessionTerminated += sessionTerminationDelegate;
            beneficiary.SessionTerminated += sessionTerminationDelegate;

            // change enum to string and add constants
            //beneficiary.TransferRequest += request => new TransferReply(VirtualAssetType.ETH, TransferType.BlockchainTransfer, "10", "1223");
            //beneficiary.TransferDispatch += message => new TransferConfirmationMessage();

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
            var session = await originator.CreateSessionAsync(originatorDoc, beneficiaryVaan);

            var transferReply = await session.TransferRequestAsync(new TransferInstruction()
            {
                VirtualAssetTransfer = new VirtualAssetTransfer()
                {
                    TransferType = TransferType.BlockchainTransfer,
                    VirtualAssetType = VirtualAssetType.ETH,
                    TransferAmount = "1000000000000000000"
                }
            });

            var transferConformation = 
                await session.TransferDispatchAsync(transferReply.Transfer, new Transaction("0xtxhash", DateTime.UtcNow, "0x0...a"));

            var beneficiarySession = beneficiary.GetActiveSessions().First() as BeneficiarySession;

            Assert.Equal(1, originator.GetActiveSessions().Count);
            Assert.True(originator.GetActiveSessions().First() is OriginatorSession);

            Assert.Equal(1, beneficiary.GetActiveSessions().Count);
            Assert.True(beneficiarySession != null);

            await session.TerminateAsync(TerminationMessage.TerminationMessageCode.SessionClosedTransferOccured);
            session.Wait();
            beneficiarySession.Wait();

            originator.Dispose();
            beneficiary.Dispose();

            Assert.Equal(2, sessionTerminationCounter);

            var pair = originatorMessageRepository.MessageStorage.First();
            var messagesReceivedByOriginator = pair.Value.ToArray();

            pair = beneficiaryMessageRepository.MessageStorage.First();
            var messagesReceivedByBeneficiary = pair.Value.ToArray();

            Assert.True(messagesReceivedByOriginator[0] is SessionReplyMessage);
            Assert.True(messagesReceivedByOriginator[1] is TransferReplyMessage);
            Assert.True(messagesReceivedByOriginator[2] is TransferConfirmationMessage);

            Assert.True(messagesReceivedByBeneficiary[0] is SessionRequestMessage);
            Assert.True(messagesReceivedByBeneficiary[1] is TransferRequestMessage);
            Assert.True(messagesReceivedByBeneficiary[2] is TransferDispatchMessage);
            Assert.True(messagesReceivedByBeneficiary[3] is TerminationMessage);


            _testOutputHelper.WriteLine("End of test");
        }
    }
}