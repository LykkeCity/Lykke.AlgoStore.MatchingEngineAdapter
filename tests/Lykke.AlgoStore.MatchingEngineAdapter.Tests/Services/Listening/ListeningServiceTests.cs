using Common.Log;
using Moq;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Tests.Services.Listening
{
    [TestFixture]
    public class ListeningServiceTests
    {
        [Test]
        public void ListeningService_AcceptsConnection_Successfully()
        {
            var producerLoadBalancer = Given_Correct_ProducerLoadBalancerMock();
            var messageQueue = Given_Correct_MessageQueue();
            var matchingEngineAdapter = Given_CorrectMatchingEngineAdapterMock();
            var logMock = Given_Log();

            var listeningService = new ListeningService(producerLoadBalancer.Object, messageQueue, matchingEngineAdapter.Object, 12345, logMock);
            listeningService.Start();

            var tcpClient = new TcpClient();
            tcpClient.Connect(new IPEndPoint(IPAddress.Loopback, 12345));

            Thread.Sleep(1000);

            listeningService.Dispose();

            producerLoadBalancer.Verify();
        }

        private Mock<IProducerLoadBalancer> Given_Correct_ProducerLoadBalancerMock()
        {
            var producerLoadBalancer = new Mock<IProducerLoadBalancer>(MockBehavior.Strict);

            producerLoadBalancer.Setup(p => p.AcceptConnection(It.IsAny<INetworkStreamWrapper>()))
                                .Verifiable();

            producerLoadBalancer.Setup(p => p.Dispose())
                                .Verifiable();

            return producerLoadBalancer;
        }

        private IMessageQueue Given_Correct_MessageQueue()
        {
            var messageQueueMock = new Mock<IMessageQueue>();
            var messageInfo = Given_Correct_MessageInfo();

            messageQueueMock.Setup(rq => rq.Dequeue(It.IsAny<CancellationToken>()))
                            .Returns(messageInfo);

            return messageQueueMock.Object;
        }

        private IMessageInfo Given_Correct_MessageInfo()
        {
            var messageInfoMock = new Mock<IMessageInfo>();

            messageInfoMock.SetupGet(ri => ri.Id)
                           .Returns(1);

            messageInfoMock.SetupGet(ri => ri.Message)
                           .Returns(new PingRequest { Message = "" });

            return messageInfoMock.Object;
        }

        private Mock<IMatchingEngineAdapter> Given_CorrectMatchingEngineAdapterMock()
        {
            var matchingEngineAdapterMock = new Mock<IMatchingEngineAdapter>(MockBehavior.Strict);
            var firstTime = true;
            var result = new ResponseModel<double>();

            matchingEngineAdapterMock.Setup(r => r.HandleMarketOrderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<OrderAction>(), It.IsAny<double>(),
                    It.IsAny<bool>(), It.IsAny<string>(), null))
                .Returns(Task.FromResult(result))
                .Callback(() =>
                {
                    if (firstTime)
                    {
                        firstTime = false;
                        return;
                    }

                    throw new OperationCanceledException();
                });

            return matchingEngineAdapterMock;
        }

        private ILog Given_Log()
        {
            var log = new Mock<ILog>();
            return log.Object;
        }
    }
}
