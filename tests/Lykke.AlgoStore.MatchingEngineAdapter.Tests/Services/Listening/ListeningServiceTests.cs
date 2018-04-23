using System;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
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
            var requestQueue = Given_Correct_RequestQueue();
            var matchingEngineAdapter = Given_CorrectMatchingEngineAdapterMock();
            var logMock = Given_Log();

            var listeningService = new ListeningService(producerLoadBalancer.Object, requestQueue, matchingEngineAdapter.Object, 12345, logMock);
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
