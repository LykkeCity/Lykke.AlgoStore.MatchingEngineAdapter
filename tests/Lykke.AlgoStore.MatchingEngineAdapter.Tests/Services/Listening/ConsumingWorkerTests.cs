using Common.Log;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Responses;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening;
using Moq;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Tests.Services.Listening
{
    [TestFixture]
    public class ConsumingWorkerTests
    {
        [Test]
        public void ConsumingWorker_SubmitsCorrectResponse_ForPingMessage()
        {
            var requestQueueMock = Given_CorrectRequestQueueMock();
            var matchingEngineAdapterMock = Given_CorrectMatchingEngineAdapterMock();
            var logMock = Given_Log();

            var consumingWorker = new ConsumingWorker(requestQueueMock.Object, matchingEngineAdapterMock.Object, logMock);
            
            Thread.Sleep(1000);

            consumingWorker.Dispose();
            requestQueueMock.Verify();
        }

        private Mock<IRequestQueue> Given_CorrectRequestQueueMock()
        {
            var requestQueueMock = new Mock<IRequestQueue>(MockBehavior.Strict);
            var firstTime = true;

            requestQueueMock.Setup(r => r.Dequeue(It.IsAny<CancellationToken>()))
                            .Returns(Given_CorrectRequestInfo())
                            .Callback(() =>
                                {
                                    if (firstTime)
                                    {
                                        firstTime = false;
                                        return;
                                    }

                                    throw new OperationCanceledException();
                                });

            return requestQueueMock;
        }

        private IRequestInfo Given_CorrectRequestInfo()
        {
            var requestInfoMock = new Mock<IRequestInfo>(MockBehavior.Strict);
            var pingRequest = new PingRequest { Message = "test" };

            requestInfoMock.SetupGet(r => r.Id)
                .Returns(1);

            requestInfoMock.SetupGet(r => r.Message)
                .Returns(pingRequest);

            requestInfoMock.Setup(r => r.Reply(MeaResponseType.Pong, pingRequest));

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
