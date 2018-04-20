using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

            var listeningService = new ListeningService(producerLoadBalancer.Object);
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
    }
}
