using Common.Log;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Responses;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening;
using Moq;
using NUnit.Framework;
using System;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Tests.Services.Listening
{
    [TestFixture]
    public class ConsumingWorkerTests
    {
        [Test]
        public void ConsumingWorker_SubmitsCorrectResponse_ForPingMessage()
        {
            var requestQueueMock = Given_CorrectRequestQueueMock();
            var logMock = Given_Log();

            var consumingWorker = new ConsumingWorker(requestQueueMock.Object, logMock);

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

        private ILog Given_Log()
        {
            var log = new Mock<ILog>();
            return log.Object;
        }
    }
}
