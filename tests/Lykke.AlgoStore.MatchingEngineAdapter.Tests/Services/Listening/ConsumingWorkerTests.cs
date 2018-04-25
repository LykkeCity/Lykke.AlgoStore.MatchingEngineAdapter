using Common.Log;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Responses;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using Moq;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Tests.Services.Listening
{
    [TestFixture]
    public class ConsumingWorkerTests
    {
        [Test]
        public void ConsumingWorker_SubmitsCorrectResponse_ForPingMessage()
        {
            var messageQueueMock = Given_CorrectMessageQueueMock();
            var matchingEngineAdapterMock = Given_CorrectMatchingEngineAdapterMock();
            var logMock = Given_Log();

            var consumingWorker = new ConsumingWorker(messageQueueMock.Object, matchingEngineAdapterMock.Object, logMock);
            
            Thread.Sleep(1000);

            consumingWorker.Dispose();
            messageQueueMock.Verify();
        }

        private Mock<IMessageQueue> Given_CorrectMessageQueueMock()
        {
            var messageQueueMock = new Mock<IMessageQueue>(MockBehavior.Strict);
            var firstTime = true;

            messageQueueMock.Setup(r => r.Dequeue(It.IsAny<CancellationToken>()))
                            .Returns(Given_CorrectMessageInfo())
                            .Callback(() =>
                                {
                                    if (firstTime)
                                    {
                                        firstTime = false;
                                        return;
                                    }

                                    throw new OperationCanceledException();
                                });

            return messageQueueMock;
        }

        private IMessageInfo Given_CorrectMessageInfo()
        {
            var messageInfoMock = new Mock<IMessageInfo>(MockBehavior.Strict);
            var pingRequest = new PingRequest { Message = "test" };

            messageInfoMock.SetupGet(r => r.Id)
                .Returns(1);

            messageInfoMock.SetupGet(r => r.Message)
                .Returns(pingRequest);

            messageInfoMock.Setup(r => r.Reply(MeaResponseType.Pong, pingRequest));

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
