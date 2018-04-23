using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Tests.Services.Listening
{
    [TestFixture]
    public class ListeningServiceTests
    {
        [Test]
        public void ListeningService_AcceptsConnection_Successfully()
        {
            var producerLoadBalancer = Given_Correct_ProducerLoadBalancerMock();
            var requestQueue = Given_Correct_RequestQueue();

            var listeningService = new ListeningService(producerLoadBalancer.Object, requestQueue, 12345);
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

            producerLoadBalancer.Setup(p => p.AcceptConnection(It.IsAny<IClientSocketWrapper>()))
                                .Verifiable();

            producerLoadBalancer.Setup(p => p.Dispose())
                                .Verifiable();

            return producerLoadBalancer;
        }

        private IRequestQueue Given_Correct_RequestQueue()
        {
            var requestQueueMock = new Mock<IRequestQueue>();
            var requestInfo = Given_Correct_RequestInfo();

            requestQueueMock.Setup(rq => rq.Dequeue(It.IsAny<CancellationToken>()))
                            .Returns(requestInfo);

            return requestQueueMock.Object;
        }

        private IRequestInfo Given_Correct_RequestInfo()
        {
            var requestInfoMock = new Mock<IRequestInfo>();

            requestInfoMock.SetupGet(ri => ri.Id)
                           .Returns(1);

            requestInfoMock.SetupGet(ri => ri.Message)
                           .Returns(new PingRequest { Message = "" });

            return requestInfoMock.Object;
        }
    }
}
