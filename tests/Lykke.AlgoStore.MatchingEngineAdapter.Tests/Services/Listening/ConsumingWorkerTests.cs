using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Responses;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Tests.Services.Listening
{
    [TestFixture]
    public class ConsumingWorkerTests
    {
        [Test]
        public void ConsumingWorker_SubmitsCorrectResponse_ForPingMessage()
        {
            var messageQueueMock = Given_CorrectMessageQueueMock();

            var consumingWorker = new ConsumingWorker(messageQueueMock.Object);

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
    }
}
