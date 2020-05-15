using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using OpenVASP.CSharpClient;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;
using Xunit;
using Transaction = OpenVASP.Messaging.Messages.Entities.Transaction;

namespace OpenVASP.Tests
{
    public class SerializationTest
    {
        private static readonly MessageEnvelope _envelope = new MessageEnvelope()
        {
            SigningKey = "0x74152a90669ef4c166a1d2b140d307181f262142486881f91a9277ee370960d9"
        };

        private readonly WhisperMessageFormatter _messageFormatter = new WhisperMessageFormatter(new NullLogger<WhisperMessageFormatter>());

        [Fact]
        public async Task TestSendingSessionRequestMessage()
        {
            var request = GetSessionRequestMessage();

            var fakeTransport = new FakeTransportClient(_messageFormatter, new WhisperSignService());
            var messageHash = await fakeTransport.SendAsync(_envelope, request);
            var receivedMessage =  (await fakeTransport.GetSessionMessagesAsync("fake")).First();

            AssertSessionRequest(receivedMessage.Message as SessionRequestMessage, request);
        }

        [Fact]
        public async Task TestSendingSessionReplyMessage()
        {
            var request = GetSessionReplyMessage();

            var fakeTransport = new FakeTransportClient(_messageFormatter, new WhisperSignService());
            var messageHash = await fakeTransport.SendAsync(_envelope, request);
            var response = (await fakeTransport.GetSessionMessagesAsync("fake")).First();

            AssertSessionReply(response.Message as SessionReplyMessage, request);
        }

        [Fact]
        public async Task TestSendingTransferRequestMessage()
        {
            var request = GetTransferRequestMessage();

            var fakeTransport = new FakeTransportClient(_messageFormatter, new WhisperSignService());
            var messageHash = await fakeTransport.SendAsync(_envelope, request);
            var response = (await fakeTransport.GetSessionMessagesAsync("fake")).First();

            AssertTransferRequest(response.Message as TransferRequestMessage, request);
        }

        [Fact]
        public async Task TestSendingTransferReplyMessage()
        {
            var request = GetTransferReplyMessage();

            var fakeTransport = new FakeTransportClient(_messageFormatter, new WhisperSignService());
            var messageHash = await fakeTransport.SendAsync(_envelope, request);
            var response = (await fakeTransport.GetSessionMessagesAsync("fake")).First();

            AssertTransferReply(response.Message as TransferReplyMessage, request);
        }

        [Fact]
        public async Task TestSendingTransferDispatchMessage()
        {
            var request = GetTransferDispatchMessage();

            var fakeTransport = new FakeTransportClient(_messageFormatter, new WhisperSignService());
            var messageHash = await fakeTransport.SendAsync(_envelope, request);
            var response = (await fakeTransport.GetSessionMessagesAsync("fake")).First();

            AssertTransferDispatch(response.Message as TransferDispatchMessage, request);
        }

        [Fact]
        public async Task TestSendingTransferConfirmationMessage()
        {
            var request = GetTransferConfirmationMessage();

            var fakeTransport = new FakeTransportClient(_messageFormatter, new WhisperSignService());
            var messageHash = await fakeTransport.SendAsync(_envelope, request);
            var response = (await fakeTransport.GetSessionMessagesAsync("fake")).First();

            AssertTransferConfirmation(response.Message as TransferConfirmationMessage, request);
        }

        [Fact]
        public async Task TestSendingTerminationMessage()
        {
            var request = GetTerminationMessage();
            var fakeTransport = new FakeTransportClient(_messageFormatter, new WhisperSignService());
            var messageHash = await fakeTransport.SendAsync(_envelope, request);
            var response = (await fakeTransport.GetSessionMessagesAsync("fake")).First();

            AssertTermination(response.Message as TerminationMessage, request);
        }

        private void AssertTermination(TerminationMessage response, TerminationMessage request)
        {
            Assert.NotNull(response);

            Assert.Equal(request.Comment, response.Comment);

            Assert.Equal(request.Message.SessionId, response.Message.SessionId);
            Assert.Equal(request.Message.MessageType, response.Message.MessageType);
            Assert.Equal(request.Message.MessageCode, response.Message.MessageCode);
            Assert.Equal(request.Message.MessageId, response.Message.MessageId);
        }

        private void AssertTransferDispatch(TransferDispatchMessage response, TransferDispatchMessage request)
        {
            Assert.NotNull(response);

            Assert.Equal(request.Comment, response.Comment);

            Assert.Equal(request.Message.SessionId, response.Message.SessionId);
            Assert.Equal(request.Message.MessageType, response.Message.MessageType);
            Assert.Equal(request.Message.MessageCode, response.Message.MessageCode);
            Assert.Equal(request.Message.MessageId, response.Message.MessageId);

            AssertTransaction(request.Transaction, response.Transaction);
        }

        private void AssertTransferConfirmation(TransferConfirmationMessage response, TransferConfirmationMessage request)
        {
            Assert.NotNull(response);

            Assert.Equal(request.Comment, response.Comment);

            Assert.Equal(request.Message.SessionId, response.Message.SessionId);
            Assert.Equal(request.Message.MessageType, response.Message.MessageType);
            Assert.Equal(request.Message.MessageCode, response.Message.MessageCode);
            Assert.Equal(request.Message.MessageId, response.Message.MessageId);
        }

        private void AssertTransaction(Transaction request, Transaction response)
        {
            Assert.Equal(request.TransactionId, response.TransactionId);
            Assert.Equal(request.DateTime.Date, response.DateTime.Date);
            Assert.Equal(request.DateTime.Hour, response.DateTime.Hour);
            Assert.Equal(request.DateTime.Minute, response.DateTime.Minute);
            Assert.Equal(request.DateTime.Second, response.DateTime.Second);
            Assert.Equal(request.SendingAddress, response.SendingAddress);
        }

        private static void AssertSessionRequest(SessionRequestMessage response, SessionRequestMessage request)
        {
            Assert.NotNull(response);

            Assert.Equal(request.HandShake.TopicA, response.HandShake.TopicA);
            Assert.Equal(request.HandShake.EcdhPubKey, response.HandShake.EcdhPubKey);

            Assert.Equal(request.Comment, response.Comment);

            Assert.Equal(request.Message.SessionId, response.Message.SessionId);
            Assert.Equal(request.Message.MessageType, response.Message.MessageType);
            Assert.Equal(request.Message.MessageCode, response.Message.MessageCode);
            Assert.Equal(request.Message.MessageId, response.Message.MessageId);

            Assert.Equal(request.Vasp.PlaceOfBirth.DateOfBirth.Date, response.Vasp.PlaceOfBirth.DateOfBirth.Date);
            Assert.Equal(request.Vasp.PlaceOfBirth.CountryOfBirth, response.Vasp.PlaceOfBirth.CountryOfBirth);
            Assert.Equal(request.Vasp.PlaceOfBirth.CityOfBirth, response.Vasp.PlaceOfBirth.CityOfBirth);

            Assert.Equal(request.Vasp.BIC, response.Vasp.BIC);
            Assert.Equal(request.Vasp.Name, response.Vasp.Name);
            Assert.Equal(request.Vasp.VaspPublickKey, response.Vasp.VaspPublickKey);
            Assert.Equal(request.Vasp.VaspIdentity, response.Vasp.VaspIdentity);

            Assert.Equal(request.Vasp.PostalAddress.StreetName, response.Vasp.PostalAddress.StreetName);
            Assert.Equal(request.Vasp.PostalAddress.AddressLine, response.Vasp.PostalAddress.AddressLine);
            Assert.Equal(request.Vasp.PostalAddress.BuildingNumber, response.Vasp.PostalAddress.BuildingNumber);
            Assert.Equal(request.Vasp.PostalAddress.Country, response.Vasp.PostalAddress.Country);
            Assert.Equal(request.Vasp.PostalAddress.PostCode, response.Vasp.PostalAddress.PostCode);

            Assert.Equal(request.Vasp.JuridicalPersonIds.Count(), response.Vasp.JuridicalPersonIds.Count());

            AssertJuridicalPersonIds(request.Vasp.JuridicalPersonIds, response.Vasp.JuridicalPersonIds);

            Assert.Equal(request.Vasp.NaturalPersonIds.Count(), response.Vasp.NaturalPersonIds.Count());

            AssertNaturalPersonIds(request.Vasp.NaturalPersonIds, response.Vasp.NaturalPersonIds);
        }

        private static void AssertSessionReply(SessionReplyMessage response, SessionReplyMessage request)
        {
            Assert.NotNull(response);

            Assert.Equal(request.HandShake.TopicB, response.HandShake.TopicB);

            Assert.Equal(request.Comment, response.Comment);

            Assert.Equal(request.Message.SessionId, response.Message.SessionId);
            Assert.Equal(request.Message.MessageType, response.Message.MessageType);
            Assert.Equal(request.Message.MessageCode, response.Message.MessageCode);
            Assert.Equal(request.Message.MessageId, response.Message.MessageId);

            Assert.Equal(request.Vasp.PlaceOfBirth.DateOfBirth.Date, response.Vasp.PlaceOfBirth.DateOfBirth.Date);
            Assert.Equal(request.Vasp.PlaceOfBirth.CountryOfBirth, response.Vasp.PlaceOfBirth.CountryOfBirth);
            Assert.Equal(request.Vasp.PlaceOfBirth.CityOfBirth, response.Vasp.PlaceOfBirth.CityOfBirth);

            Assert.Equal(request.Vasp.BIC, response.Vasp.BIC);
            Assert.Equal(request.Vasp.Name, response.Vasp.Name);
            Assert.Equal(request.Vasp.VaspPublickKey, response.Vasp.VaspPublickKey);
            Assert.Equal(request.Vasp.VaspIdentity, response.Vasp.VaspIdentity);

            Assert.Equal(request.Vasp.PostalAddress.StreetName, response.Vasp.PostalAddress.StreetName);
            Assert.Equal(request.Vasp.PostalAddress.AddressLine, response.Vasp.PostalAddress.AddressLine);
            Assert.Equal(request.Vasp.PostalAddress.BuildingNumber, response.Vasp.PostalAddress.BuildingNumber);
            Assert.Equal(request.Vasp.PostalAddress.Country, response.Vasp.PostalAddress.Country);
            Assert.Equal(request.Vasp.PostalAddress.PostCode, response.Vasp.PostalAddress.PostCode);

            Assert.Equal(request.Vasp.JuridicalPersonIds.Count(), response.Vasp.JuridicalPersonIds.Count());

            AssertJuridicalPersonIds(request.Vasp.JuridicalPersonIds, response.Vasp.JuridicalPersonIds);

            Assert.Equal(request.Vasp.NaturalPersonIds.Count(), response.Vasp.NaturalPersonIds.Count());

            AssertNaturalPersonIds(request.Vasp.NaturalPersonIds, response.Vasp.NaturalPersonIds);
        }

        private static void AssertTransferRequest(TransferRequestMessage response, TransferRequestMessage request)
        {
            Assert.NotNull(response);

            Assert.Equal(request.Comment, response.Comment);

            Assert.Equal(request.Message.SessionId, response.Message.SessionId);
            Assert.Equal(request.Message.MessageType, response.Message.MessageType);
            Assert.Equal(request.Message.MessageCode, response.Message.MessageCode);
            Assert.Equal(request.Message.MessageId, response.Message.MessageId);

            Assert.Equal(request.Transfer.TransferType, response.Transfer.TransferType);
            Assert.Equal(request.Transfer.VirtualAssetType, response.Transfer.VirtualAssetType);
            Assert.Equal(request.Transfer.Amount, response.Transfer.Amount);

            AssertBeneficiary(request.Beneficiary, response.Beneficiary);

            AssertOriginator(request.Originator, response.Originator);
        }

        private static void AssertTransferReply(TransferReplyMessage response, TransferReplyMessage request)
        {
            Assert.NotNull(response);

            Assert.Equal(request.Comment, response.Comment);

            Assert.Equal(request.Message.SessionId, response.Message.SessionId);
            Assert.Equal(request.Message.MessageType, response.Message.MessageType);
            Assert.Equal(request.Message.MessageCode, response.Message.MessageCode);
            Assert.Equal(request.Message.MessageId, response.Message.MessageId);
        }

        private static void AssertPlaceOfBirth(PlaceOfBirth request, PlaceOfBirth response)
        {
            Assert.Equal(request.DateOfBirth.Date, response.DateOfBirth.Date);
            Assert.Equal(request.CountryOfBirth, response.CountryOfBirth);
            Assert.Equal(request.CityOfBirth, response.CityOfBirth);
        }

        private static void AssertOriginator(Originator requestOriginator, Originator responseOriginator)
        {
            Assert.Equal(requestOriginator.BIC, requestOriginator.BIC);
            Assert.Equal(requestOriginator.VAAN, requestOriginator.VAAN);
            Assert.Equal(requestOriginator.Name, requestOriginator.Name);

            AssertPlaceOfBirth(requestOriginator.PlaceOfBirth, responseOriginator.PlaceOfBirth);
            AssertPostalAddress(requestOriginator.PostalAddress, responseOriginator.PostalAddress);

            AssertJuridicalPersonIds(requestOriginator.JuridicalPersonId, responseOriginator.JuridicalPersonId);
            AssertNaturalPersonIds(requestOriginator.NaturalPersonId, responseOriginator.NaturalPersonId);
        }

        private static void AssertPostalAddress(PostalAddress request, PostalAddress response)
        {
            Assert.Equal(request.StreetName, response.StreetName);
            Assert.Equal(request.AddressLine, response.AddressLine);
            Assert.Equal(request.BuildingNumber, response.BuildingNumber);
            Assert.Equal(request.Country, response.Country);
            Assert.Equal(request.PostCode, response.PostCode);
        }

        private static void AssertBeneficiary(Beneficiary requestBeneficiary, Beneficiary responseBeneficiary)
        {
            Assert.Equal(requestBeneficiary.Name, responseBeneficiary.Name);
            Assert.Equal(requestBeneficiary.VAAN, responseBeneficiary.VAAN);
        }

        private static void AssertNaturalPersonIds(NaturalPersonId[] request, NaturalPersonId[] response)
        {
            for (int i = 0; i < request.Count(); i++)
            {
                var expected = request[i];
                var actual = response[i];

                Assert.Equal(expected.IssuingCountry, actual.IssuingCountry);
                Assert.Equal(expected.IdentificationType, actual.IdentificationType);
                Assert.Equal(expected.Identifier, actual.Identifier);
                Assert.Equal(expected.NonStateIssuer, actual.NonStateIssuer);
            }
        }

        private static void AssertJuridicalPersonIds(JuridicalPersonId[] request, JuridicalPersonId[] response)
        {
            for (int i = 0; i < request.Count(); i++)
            {
                var expected = request[i];
                var actual = response[i];

                Assert.Equal(expected.IssuingCountry, actual.IssuingCountry);
                Assert.Equal(expected.IdentificationType, actual.IdentificationType);
                Assert.Equal(expected.Identifier, actual.Identifier);
                Assert.Equal(expected.NonStateIssuer, actual.NonStateIssuer);
            }
        }

        private static SessionRequestMessage GetSessionRequestMessage()
        {
            //Should be a contract
            var vaspKey = EthECKey.GenerateKey();

            //4Bytes 
            var topic = "0x12345678"; //"0x" + "My Topic".GetHashCode().ToString("x");

            string ecdhPubKey = "12";

            var message = new Message(
                Guid.NewGuid().ToByteArray().ToHex(prefix: false),
                Guid.NewGuid().ToByteArray().ToHex(prefix: false),
                "1", MessageType.SessionRequest);
            var handshake = new HandShakeRequest(topic, ecdhPubKey);
            var postalAddress = new PostalAddress(
                "TestingStreet",
                61,
                "Test Address Line",
                "410000",
                "TownN",
                Country.List["DE"]
            );
            var placeOfBirth = new PlaceOfBirth(DateTime.UtcNow, "TownN", Country.List["DE"]);
            var vaspInformation = new VaspInformation(
                "Test test",
                vaspKey.GetPublicAddress(),
                vaspKey.GetPubKey().ToHex(prefix: false),
                postalAddress,
                placeOfBirth,
                new NaturalPersonId[]
                {
                    new NaturalPersonId("SomeId2", NaturalIdentificationType.AlienRegistrationNumber,
                        Country.List["DE"]),
                },
                new JuridicalPersonId[]
                {
                    new JuridicalPersonId("SomeId1", JuridicalIdentificationType.BankPartyIdentification,
                        Country.List["DE"]),
                },
                "DEUTDEFF");

            var request = SessionRequestMessage.Create(message, handshake, vaspInformation);
            request.Comment = "This is test message";
            
            return request;
        }

        private static SessionReplyMessage GetSessionReplyMessage()
        {
            //Should be a contract
            var vaspKey = EthECKey.GenerateKey();

            //4Bytes 
            var topic = "0x12345678"; //"0x" + "My Topic".GetHashCode().ToString("x");

            var message = new Message(
                Guid.NewGuid().ToByteArray().ToHex(prefix: false),
                Guid.NewGuid().ToByteArray().ToHex(prefix: false),
                "1", MessageType.SessionReply);
            var handshake = new HandShakeResponse(topic);
            var postalAddress = new PostalAddress(
                "TestingStreet",
                61,
                "Test Address Line",
                "410000",
                "TownN",
                Country.List["DE"]
            );
            var placeOfBirth = new PlaceOfBirth(DateTime.UtcNow, "TownN", Country.List["DE"]);
            var vaspInformation = new VaspInformation(
                "Test test",
                vaspKey.GetPublicAddress(),
                vaspKey.GetPubKey().ToHex(prefix: false),
                postalAddress,
                placeOfBirth,
                new NaturalPersonId[]
                {
                    new NaturalPersonId("SomeId2", NaturalIdentificationType.AlienRegistrationNumber,
                        Country.List["DE"]),
                },
                new JuridicalPersonId[]
                {
                    new JuridicalPersonId("SomeId1", JuridicalIdentificationType.BankPartyIdentification,
                        Country.List["DE"]),
                },
                "DEUTDEFF");

            var request = SessionReplyMessage.Create(message, handshake, vaspInformation);
            request.Comment = "This is test message";
            
            return request;
        }

        private static TransferRequestMessage GetTransferRequestMessage()
        {
            //Should be a contract
            var vaspKey = EthECKey.GenerateKey();

            var message = new Message(
                Guid.NewGuid().ToByteArray().ToHex(prefix: false),
                Guid.NewGuid().ToByteArray().ToHex(prefix: false),
                "1", MessageType.TransferRequest);

            var postalAddress = new PostalAddress(
                "TestingStreet",
                61,
                "Test Address Line",
                "410000",
                "TownN",
                Country.List["DE"]
            );
            var placeOfBirth = new PlaceOfBirth(DateTime.UtcNow, "TownN", Country.List["DE"]);
            var vaspInformation = new VaspInformation(
                "Test test",
                vaspKey.GetPublicAddress(),
                vaspKey.GetPubKey().ToHex(prefix: false),
                postalAddress,
                placeOfBirth,
                new NaturalPersonId[]
                {
                    new NaturalPersonId("SomeId2", NaturalIdentificationType.AlienRegistrationNumber,
                        Country.List["DE"]),
                },
                new JuridicalPersonId[]
                {
                    new JuridicalPersonId("SomeId1", JuridicalIdentificationType.BankPartyIdentification,
                        Country.List["DE"]),
                },
                "DEUTDEFF");

            var originator = new Originator("Originator1", "VaaN", postalAddress, placeOfBirth,
                new NaturalPersonId[]
                {
                    new NaturalPersonId("SomeId2", NaturalIdentificationType.AlienRegistrationNumber,
                        Country.List["DE"]),
                },
                new JuridicalPersonId[]
                {
                    new JuridicalPersonId("SomeId1", JuridicalIdentificationType.BankPartyIdentification,
                        Country.List["DE"]),
                },
                "DEUTDEFF");

            var beneficiary = new Beneficiary("Ben1", "VaaN");

            var transferRequest = new TransferRequest(VirtualAssetType.ETH, TransferType.BlockchainTransfer, 10000000);

            var request =
                TransferRequestMessage.Create(message, originator, beneficiary, transferRequest);
            request.Comment = "This is test message";

            return request;
        }

        private static TransferReplyMessage GetTransferReplyMessage()
        {
            //Should be a contract
            var vaspKey = EthECKey.GenerateKey();

            var message = new Message(
                Guid.NewGuid().ToByteArray().ToHex(prefix: false),
                Guid.NewGuid().ToByteArray().ToHex(prefix: false),
                "1", MessageType.TransferReply);

            var postalAddress = new PostalAddress(
                "TestingStreet",
                61,
                "Test Address Line",
                "410000",
                "TownN",
                Country.List["DE"]
            );
            var placeOfBirth = new PlaceOfBirth(DateTime.UtcNow, "TownN", Country.List["DE"]);
            var vaspInformation = new VaspInformation(
                "Test test",
                vaspKey.GetPublicAddress(),
                vaspKey.GetPubKey().ToHex(prefix: false),
                postalAddress,
                placeOfBirth,
                new NaturalPersonId[]
                {
                    new NaturalPersonId("SomeId2", NaturalIdentificationType.AlienRegistrationNumber,
                        Country.List["DE"]),
                },
                new JuridicalPersonId[]
                {
                    new JuridicalPersonId("SomeId1", JuridicalIdentificationType.BankPartyIdentification,
                        Country.List["DE"]),
                },
                "DEUTDEFF");

            var originator = new Originator("Originator1", "VaaN", postalAddress, placeOfBirth,
                new NaturalPersonId[]
                {
                    new NaturalPersonId("SomeId2", NaturalIdentificationType.AlienRegistrationNumber,
                        Country.List["DE"]),
                },
                new JuridicalPersonId[]
                {
                    new JuridicalPersonId("SomeId1", JuridicalIdentificationType.BankPartyIdentification,
                        Country.List["DE"]),
                },
                "DEUTDEFF");

            var beneficiary = new Beneficiary("Ben1", "VaaN");

            var transferReply = new TransferReply(VirtualAssetType.ETH, TransferType.BlockchainTransfer, 10000000, "0x0000001");

            var request = TransferReplyMessage.Create(message, "destinatinoAddress");
            request.Comment = "This is test message";

            return request;
        }

        private static TransferDispatchMessage GetTransferDispatchMessage()
        {
            //Should be a contract
            var vaspKey = EthECKey.GenerateKey();

            var message = new Message(
                Guid.NewGuid().ToByteArray().ToHex(prefix: false),
                Guid.NewGuid().ToByteArray().ToHex(prefix: false),
                "1", MessageType.TransferDispatch);

            var postalAddress = new PostalAddress(
                "TestingStreet",
                61,
                "Test Address Line",
                "410000",
                "TownN",
                Country.List["DE"]
            );
            var placeOfBirth = new PlaceOfBirth(DateTime.UtcNow, "TownN", Country.List["DE"]);
            var vaspInformation = new VaspInformation(
                "Test test",
                vaspKey.GetPublicAddress(),
                vaspKey.GetPubKey().ToHex(prefix: false),
                postalAddress,
                placeOfBirth,
                new NaturalPersonId[]
                {
                    new NaturalPersonId("SomeId2", NaturalIdentificationType.AlienRegistrationNumber,
                        Country.List["DE"]),
                },
                new JuridicalPersonId[]
                {
                    new JuridicalPersonId("SomeId1", JuridicalIdentificationType.BankPartyIdentification,
                        Country.List["DE"]),
                },
                "DEUTDEFF");

            var originator = new Originator("Originator1", "VaaN", postalAddress, placeOfBirth,
                new NaturalPersonId[]
                {
                    new NaturalPersonId("SomeId2", NaturalIdentificationType.AlienRegistrationNumber,
                        Country.List["DE"]),
                },
                new JuridicalPersonId[]
                {
                    new JuridicalPersonId("SomeId1", JuridicalIdentificationType.BankPartyIdentification,
                        Country.List["DE"]),
                },
                "DEUTDEFF");

            var beneficiary = new Beneficiary("Ben1", "VaaN");

            var transferReply = new TransferReply(VirtualAssetType.ETH, TransferType.BlockchainTransfer, 10000000, "0x0000001");
            var transaction = new Transaction("txId", "0x0000002");

            var request = TransferDispatchMessage.Create(message, transaction);
            request.Comment = "This is test message";

            return request;
        }

        private static TransferConfirmationMessage GetTransferConfirmationMessage()
        {
            //Should be a contract
            var vaspKey = EthECKey.GenerateKey();

            var message = new Message(
                Guid.NewGuid().ToByteArray().ToHex(prefix: false),
                Guid.NewGuid().ToByteArray().ToHex(prefix: false),
                "1", MessageType.TransferConfirmation);

            var postalAddress = new PostalAddress(
                "TestingStreet",
                61,
                "Test Address Line",
                "410000",
                "TownN",
                Country.List["DE"]
            );
            var placeOfBirth = new PlaceOfBirth(DateTime.UtcNow, "TownN", Country.List["DE"]);
            var vaspInformation = new VaspInformation(
                "Test test",
                vaspKey.GetPublicAddress(),
                vaspKey.GetPubKey().ToHex(prefix: false),
                postalAddress,
                placeOfBirth,
                new NaturalPersonId[]
                {
                    new NaturalPersonId("SomeId2", NaturalIdentificationType.AlienRegistrationNumber,
                        Country.List["DE"]),
                },
                new JuridicalPersonId[]
                {
                    new JuridicalPersonId("SomeId1", JuridicalIdentificationType.BankPartyIdentification,
                        Country.List["DE"]),
                },
                "DEUTDEFF");

            var originator = new Originator("Originator1", "VaaN", postalAddress, placeOfBirth,
                new NaturalPersonId[]
                {
                    new NaturalPersonId("SomeId2", NaturalIdentificationType.AlienRegistrationNumber,
                        Country.List["DE"]),
                },
                new JuridicalPersonId[]
                {
                    new JuridicalPersonId("SomeId1", JuridicalIdentificationType.BankPartyIdentification,
                        Country.List["DE"]),
                },
                "DEUTDEFF");

            var beneficiary = new Beneficiary("Ben1", "VaaN");

            var transferReply = new TransferReply(VirtualAssetType.ETH, TransferType.BlockchainTransfer, 10000000, "0x0000001");
            var transaction = new Transaction("txId", "0x0000002");

            var request = TransferConfirmationMessage.Create(message);
            request.Comment = "This is test message";

            return request;
        }

        private static TerminationMessage GetTerminationMessage()
        {
            //Should be a contract
            var vaspKey = EthECKey.GenerateKey();

            var message = new Message(
                Guid.NewGuid().ToByteArray().ToHex(prefix: false),
                Guid.NewGuid().ToByteArray().ToHex(prefix: false),
                "1", MessageType.Termination);
            var postalAddress = new PostalAddress(
                "TestingStreet",
                61,
                "Test Address Line",
                "410000",
                "TownN",
                Country.List["DE"]
            );
            var placeOfBirth = new PlaceOfBirth(DateTime.UtcNow, "TownN", Country.List["DE"]);
            var vaspInformation = new VaspInformation(
                "Test test",
                vaspKey.GetPublicAddress(),
                vaspKey.GetPubKey().ToHex(prefix: false),
                postalAddress,
                placeOfBirth,
                new NaturalPersonId[]
                {
                    new NaturalPersonId("SomeId2", NaturalIdentificationType.AlienRegistrationNumber,
                        Country.List["DE"]),
                },
                new JuridicalPersonId[]
                {
                    new JuridicalPersonId("SomeId1", JuridicalIdentificationType.BankPartyIdentification,
                        Country.List["DE"]),
                },
                "DEUTDEFF");

            var request = TerminationMessage.Create(message);
            request.Comment = "This is test message";
            return request;
        }
    }
}
