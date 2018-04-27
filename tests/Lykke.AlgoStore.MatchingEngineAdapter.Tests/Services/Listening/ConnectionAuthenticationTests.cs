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
using System;
using System.Collections.Concurrent;
using System.Threading;
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
                new PingRequest { Message = $"{UNKNOWN_GUID_PLACEHOLDER}_{UNKNOWN_GUID_PLACEHOLDER}" },
                (byte)MeaRequestType.Ping);
        }

        [Test]
        public void ProducingWorker_ClosesConnection_WhenAlgoInstanceNotStarted()
        {
            AssertRequestDisconnects(
                new PingRequest { Message = $"{KNOWN_GUID_PLACEHOLDER}_{KNOWN_GUID_PLACEHOLDER}" },
                (byte)MeaRequestType.Ping);
        }

        [Test]
        public void ProducingWorker_ClosesConnection_WhenDuplicateConnection()
        {
            var request = new PingRequest { Message = $"{STARTED_GUID_PLACEHOLDER}_{STARTED_GUID_PLACEHOLDER}" };

            var log = Given_Correct_Log();
            var connectionSet = new ConcurrentDictionary<string, byte>();

            var asyncResultMock = Given_Verifiable_AsyncResultMock();
            var messageInfoMock = Given_Verifiable_MessageInfoMock(request, false);
            var listenerNetworkStreamMock =
                Given_Verifiable_ListenerNetworkStreamWrapperMock(asyncResultMock.Object, messageInfoMock.Object, false);

            var secondAsyncResultMock = Given_Verifiable_AsyncResultMock();
            var failingMessageInfoMock = Given_Verifiable_MessageInfoMock(request, true);
            var secondListenerNetworkStreamMock =
                Given_Verifiable_ListenerNetworkStreamWrapperMock(secondAsyncResultMock.Object,
                                                                  failingMessageInfoMock.Object, true);

            var producingWorker = Given_Correct_ProducingWorker(connectionSet, log);

            producingWorker.AddConnection(listenerNetworkStreamMock.Object);

            Thread.Sleep(3000);

            producingWorker.AddConnection(secondListenerNetworkStreamMock.Object);

            Thread.Sleep(3000);

            listenerNetworkStreamMock.Verify();
            secondListenerNetworkStreamMock.Verify();

            messageInfoMock.Verify();
            failingMessageInfoMock.Verify();

            asyncResultMock.Verify();
            secondAsyncResultMock.Verify();
        }

        [Test]
        public void ProducingWorker_KeepsConnection_WhenAuthenticationCorrect()
        { 
            var log = Given_Correct_Log();

            var connectionSet = new ConcurrentDictionary<string, byte>();
            var request = new PingRequest { Message = $"{STARTED_GUID_PLACEHOLDER}_{STARTED_GUID_PLACEHOLDER}" };
            var asyncResultMock = Given_Verifiable_AsyncResultMock();
            var messageInfoMock = Given_Verifiable_MessageInfoMock(request, false);
            var listenerNetworkStreamMock =
                Given_Verifiable_ListenerNetworkStreamWrapperMock(asyncResultMock.Object, messageInfoMock.Object, false);

            var producingWorker = Given_Correct_ProducingWorker(connectionSet, log);

            producingWorker.AddConnection(listenerNetworkStreamMock.Object);

            Thread.Sleep(250);

            listenerNetworkStreamMock.Verify();
            messageInfoMock.Verify();
            asyncResultMock.Verify();
        }

        private void AssertRequestDisconnects<T>(T request, byte requestType)
        {
            var log = Given_Correct_Log();

            var connectionSet = new ConcurrentDictionary<string, byte>();
            var asyncResultMock = Given_Verifiable_AsyncResultMock();
            var messageInfoMock = Given_Verifiable_MessageInfoMock(request, true);
            var listenerNetworkStreamMock = 
                Given_Verifiable_ListenerNetworkStreamWrapperMock(asyncResultMock.Object, messageInfoMock.Object, true);

            var producingWorker = Given_Correct_ProducingWorker(connectionSet, log);

            producingWorker.AddConnection(listenerNetworkStreamMock.Object);

            Thread.Sleep(250);

            listenerNetworkStreamMock.Verify();
            messageInfoMock.Verify();
            asyncResultMock.Verify();
        }

        private ILog Given_Correct_Log()
        {
            var logMock = new Mock<ILog>();
            return logMock.Object;
        }

        private Mock<IAsyncResult> Given_Verifiable_AsyncResultMock()
        {
            var autoReset = new AutoResetEvent(true);

            var asyncResultMock = new Mock<IAsyncResult>(MockBehavior.Strict);
            asyncResultMock.SetupGet(a => a.AsyncWaitHandle)
                           .Returns(autoReset)
                           .Verifiable();

            return asyncResultMock;
        }

        private Mock<IMessageInfo> Given_Verifiable_MessageInfoMock(object messageToReturn, bool replyShouldFail)
        {
            var messageInfoMock = new Mock<IMessageInfo>(MockBehavior.Strict);

            messageInfoMock.SetupGet(m => m.Message)
                           .Returns(messageToReturn)
                           .Verifiable();

            messageInfoMock.Setup(m => m.Reply(MeaResponseType.Pong, It.IsAny<PingRequest>()))
                           .Callback((MeaResponseType response, PingRequest request) =>
                           {
                               if (replyShouldFail)
                                   Assert.AreEqual("Fail", request.Message);
                               else
                                   Assert.AreEqual("Success", request.Message);
                           })
                           .Verifiable();

            return messageInfoMock;
        }

        private Mock<INetworkStreamWrapper> Given_Verifiable_ListenerNetworkStreamWrapperMock(
            IAsyncResult asyncResult,
            IMessageInfo messageInfo,
            bool shouldFail)
        {
            var listenerNetworkStreamMock = new Mock<INetworkStreamWrapper>(MockBehavior.Strict);
            var isAuthenticated = false;
            var id = "";

            if (shouldFail)
            {
                listenerNetworkStreamMock.Setup(l => l.Dispose())
                                         .Verifiable();
            }

            listenerNetworkStreamMock.Setup(l => l.ID)
                                     .Returns(() => id);

            listenerNetworkStreamMock.SetupSet(l => l.ID = It.IsAny<string>())
                                     .Callback((string val) => id = val);

            listenerNetworkStreamMock.Setup(l => l.BeginReadMessage(It.IsAny<AsyncCallback>(), It.IsAny<object>()))
                                     .Returns(asyncResult)
                                     .Verifiable();

            listenerNetworkStreamMock.Setup(l => l.EndReadMessage(It.IsAny<IAsyncResult>()))
                                     .Returns(messageInfo)
                                     .Verifiable();

            if (!shouldFail)
            {
                listenerNetworkStreamMock.Setup(l => l.MarkAuthenticated())
                                         .Callback(() => isAuthenticated = true)
                                         .Verifiable();
            }

            listenerNetworkStreamMock.SetupGet(l => l.IsAuthenticated)
                                     .Returns(() => isAuthenticated)
                                     .Verifiable();

            listenerNetworkStreamMock.SetupGet(l => l.AuthenticationEnabled)
                                     .Returns(true)
                                     .Verifiable();

            return listenerNetworkStreamMock;
        }

        private IAlgoClientInstanceRepository Given_Correct_AlgoClientInstanceRepository()
        {
            var algoClientInstanceRepoMock = new Mock<IAlgoClientInstanceRepository>();

            algoClientInstanceRepoMock
                .Setup(repo => repo.ExistsAlgoInstanceDataWithClientIdAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string clientId, string instanceId) => 
                {
                    if (clientId == UNKNOWN_GUID_PLACEHOLDER && instanceId == UNKNOWN_GUID_PLACEHOLDER)
                        return false;
                    else if (clientId == KNOWN_GUID_PLACEHOLDER && instanceId == KNOWN_GUID_PLACEHOLDER)
                        return true;
                    else if (clientId == STARTED_GUID_PLACEHOLDER && instanceId == STARTED_GUID_PLACEHOLDER)
                        return true;

                    return false;
                });

            algoClientInstanceRepoMock
                .Setup(repo => repo.GetAlgoInstanceDataByClientIdAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string clientId, string instanceId) =>
                {
                    if (clientId == KNOWN_GUID_PLACEHOLDER && instanceId == KNOWN_GUID_PLACEHOLDER)
                        return new AlgoClientInstanceData { AlgoInstanceStatus = AlgoInstanceStatus.Stopped };
                    else if (clientId == STARTED_GUID_PLACEHOLDER && instanceId == STARTED_GUID_PLACEHOLDER)
                        return new AlgoClientInstanceData { AlgoInstanceStatus = AlgoInstanceStatus.Started };

                    return null;
                });

            return algoClientInstanceRepoMock.Object;
        }

        private ProducingWorker Given_Correct_ProducingWorker(ConcurrentDictionary<string, byte> connectionSet, ILog log)
        {
            var messageQueueMock = new Mock<IMessageQueue>();
            var algoClientInstanceRepo = Given_Correct_AlgoClientInstanceRepository();

            messageQueueMock.Setup(m => m.Enqueue(It.IsAny<IMessageInfo>()))
                            .Callback<IMessageInfo>((request) => request.Reply(MeaResponseType.Pong, request.Message));

            return new ProducingWorker(messageQueueMock.Object, algoClientInstanceRepo, connectionSet, log);
        }
    }
}
