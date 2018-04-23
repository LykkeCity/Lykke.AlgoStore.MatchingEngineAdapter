using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Common.Log;

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
            var logMock = Given_Log();

            var listeningService = new ListeningService(producerLoadBalancer.Object, messageQueue, 12345, logMock);
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

        private ILog Given_Log()
        {
            var log = new Mock<ILog>();
            return log.Object;
        }
    }
}
