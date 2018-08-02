using Common.Log;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Enumerators;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Models;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Repositories;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Responses;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening;
using Moq;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using MarketOrderRequest = Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests.MarketOrderRequest;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Tests.Services.Listening
{
    [TestFixture]
    public class ConnectionAuthenticationTests
    {
        private const string UNKNOWN_GUID_PLACEHOLDER = "guid";
        private const string KNOWN_GUID_PLACEHOLDER = "knownguid";
        private const string STARTED_GUID_PLACEHOLDER = "startedguid";

        [Test]
        public void ProducingWorker_ClosesConnection_WhenInvalidRequestSent()
        {
            AssertRequestDisconnects(new MarketOrderRequest(), (byte)MeaRequestType.MarketOrderRequest);
        }

        [Test]
        public void ProducingWorker_ClosesConnection_WhenEmptyPingRequestSent()
        {
            AssertRequestDisconnects(new PingRequest(), (byte)MeaRequestType.Ping);
        }
        
        [Test]
        public void ProducingWorker_ClosesConnection_WhenInvalidPingRequestSent()
        {
            AssertRequestDisconnects(new PingRequest { Message = "_invalidguid" }, (byte)MeaRequestType.Ping);
        }

        [Test]
        public void ProducingWorker_ClosesConnection_WhenUnknownGuidsSent()
        {
            AssertRequestDisconnects(
                new PingRequest { Message = $"{UNKNOWN_GUID_PLACEHOLDER}" },
                (byte)MeaRequestType.Ping);
        }

        [Test]
        public void ProducingWorker_ClosesConnection_WhenAlgoInstanceNotStarted()
        {
            AssertRequestDisconnects(
                new PingRequest { Message = $"{KNOWN_GUID_PLACEHOLDER}" },
                (byte)MeaRequestType.Ping);
        }

        [Test]
        public void ProducingWorker_ClosesConnection_WhenDuplicateConnection()
        {
            var request = new PingRequest { Message = $"{STARTED_GUID_PLACEHOLDER}" };

            var log = Given_Correct_Log();

            var messageInfoMock = Given_Verifiable_MessageInfoMock(request, true);
            var streamWrapperMock = Given_Verifiable_StreamWrapperMock(messageInfoMock.Object, true);

            var producingWorker = Given_Correct_ConnectionWorker(streamWrapperMock.Object, log, false);

            producingWorker.AcceptMessagesAsync(CancellationToken.None).Wait();

            streamWrapperMock.Verify();
            messageInfoMock.Verify();
        }

        [Test]
        public void ProducingWorker_KeepsConnection_WhenAuthenticationCorrect()
        { 
            var log = Given_Correct_Log();

            var request = new PingRequest { Message = $"{STARTED_GUID_PLACEHOLDER}" };
            var messageInfoMock = Given_Verifiable_MessageInfoMock(request, false);
            var streamWrapperMock = Given_Verifiable_StreamWrapperMock(messageInfoMock.Object, false);

            var producingWorker = Given_Correct_ConnectionWorker(streamWrapperMock.Object, log, true);

            producingWorker.AcceptMessagesAsync(CancellationToken.None);

            Thread.Sleep(250);

            streamWrapperMock.Verify();
            messageInfoMock.Verify();
        }

        private void AssertRequestDisconnects<T>(T request, byte requestType)
        {
            var log = Given_Correct_Log();

            var messageInfoMock = Given_Verifiable_MessageInfoMock(request, true);
            var streamWrapperMock = Given_Verifiable_StreamWrapperMock(messageInfoMock.Object, true);

            var producingWorker = Given_Correct_ConnectionWorker(streamWrapperMock.Object, log, true);

            producingWorker.AcceptMessagesAsync(CancellationToken.None).Wait();

            streamWrapperMock.Verify();
            messageInfoMock.Verify();
        }

        private ILog Given_Correct_Log()
        {
            var logMock = new Mock<ILog>();
            return logMock.Object;
        }

        private Mock<IMessageInfo> Given_Verifiable_MessageInfoMock(object messageToReturn, bool replyShouldFail)
        {
            var messageInfoMock = new Mock<IMessageInfo>(MockBehavior.Strict);

            messageInfoMock.SetupGet(m => m.Message)
                           .Returns(messageToReturn)
                           .Verifiable();

            var setup = messageInfoMock.Setup(m => m.ReplyAsync(MeaResponseType.Pong, It.IsAny<PingRequest>()))
                                       .Returns(Task.CompletedTask);
            setup.Callback((MeaResponseType response, PingRequest request) =>
            {
                if (replyShouldFail)
                    Assert.AreEqual("Fail", request.Message);
                else
                    Assert.AreEqual("Success", request.Message);
            });
            setup.Verifiable();

            return messageInfoMock;
        }

        private Mock<IStreamWrapper> Given_Verifiable_StreamWrapperMock(
            IMessageInfo messageInfo,
            bool shouldFail)
        {
            var listenerStreamMock = new Mock<IStreamWrapper>(MockBehavior.Strict);
            var isAuthenticated = false;
            var id = "";

            if (shouldFail)
            {
                listenerStreamMock.Setup(l => l.Dispose())
                                         .Verifiable();
            }

            listenerStreamMock.Setup(l => l.ID)
                                     .Returns(() => id);

            listenerStreamMock.SetupSet(l => l.ID = It.IsAny<string>())
                                     .Callback((string val) => id = val);

            listenerStreamMock.Setup(l => l.ReadMessageAsync())
                              .ReturnsAsync(messageInfo)
                              .Verifiable();

            if (!shouldFail)
            {
                listenerStreamMock.Setup(l => l.MarkAuthenticated())
                                         .Callback(() => isAuthenticated = true)
                                         .Verifiable();
            }

            listenerStreamMock.SetupGet(l => l.IsAuthenticated)
                                     .Returns(() => isAuthenticated)
                                     .Verifiable();

            listenerStreamMock.SetupGet(l => l.AuthenticationEnabled)
                                     .Returns(true)
                                     .Verifiable();

            return listenerStreamMock;
        }

        private IAlgoClientInstanceRepository Given_Correct_AlgoClientInstanceRepository()
        {
            var algoClientInstanceRepoMock = new Mock<IAlgoClientInstanceRepository>();

            algoClientInstanceRepoMock
                .Setup(repo => repo.ExistsAlgoInstanceDataWithAuthTokenAsync(It.IsAny<string>()))
                .ReturnsAsync((string authToken) => 
                {
                    if (authToken == UNKNOWN_GUID_PLACEHOLDER)
                        return false;
                    else if (authToken == KNOWN_GUID_PLACEHOLDER)
                        return true;
                    else if (authToken == STARTED_GUID_PLACEHOLDER)
                        return true;

                    return false;
                });

            algoClientInstanceRepoMock
                .Setup(repo => repo.GetAlgoInstanceDataByAuthTokenAsync(It.IsAny<string>()))
                .ReturnsAsync((string authToken) =>
                {
                    if (authToken == KNOWN_GUID_PLACEHOLDER)
                        return new AlgoClientInstanceData { AlgoInstanceStatus = AlgoInstanceStatus.Stopped };
                    else if (authToken == STARTED_GUID_PLACEHOLDER)
                        return new AlgoClientInstanceData { AlgoInstanceStatus = AlgoInstanceStatus.Started };

                    return null;
                });

            return algoClientInstanceRepoMock.Object;
        }

        private ConnectionWorker Given_Correct_ConnectionWorker(IStreamWrapper streamWrapper, ILog log, bool shouldAuthenticate)
        {
            var messageHandlerMock = new Mock<IMessageHandler>();
            var algoClientInstanceRepo = Given_Correct_AlgoClientInstanceRepository();

            messageHandlerMock.Setup(m => m.HandleMessage(It.IsAny<IMessageInfo>()))
                            .Callback<IMessageInfo>((request) => request.ReplyAsync(MeaResponseType.Pong, request.Message).Wait());

            return new ConnectionWorker(streamWrapper, messageHandlerMock.Object, algoClientInstanceRepo, log,
                (conn, str) => Task.FromResult(shouldAuthenticate));
        }
    }
}
