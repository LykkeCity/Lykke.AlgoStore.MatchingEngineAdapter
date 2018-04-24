using Common.Log;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Enumerators;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Models;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Repositories;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Responses;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Sockets;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Tests.Services.Listening
{
    [TestFixture]
    public class ConnectionAuthenticationTests
    {
        private const string UNKNOWN_GUID_PLACEHOLDER = "guid";
        private const string KNOWN_GUID_PLACEHOLDER = "knownguid";
        private const string STARTED_GUID_PLACEHOLDER = "startedguid";

        private TcpListener _tcpListener;
        private TcpClient _tcpClient;

        private NetworkStream _listenerStream;
        private NetworkStream _clientStream;

        [SetUp]
        public void SetupConnections()
        {
            _tcpListener = new TcpListener(IPAddress.Any, 23456);
            _tcpListener.Start();

            _tcpClient = new TcpClient();
            _tcpClient.Connect(IPAddress.Loopback, 23456);

            _listenerStream = new NetworkStream(_tcpListener.AcceptSocket(), true);
            _clientStream = _tcpClient.GetStream();
        }

        [TearDown]
        public void CleanupConnections()
        {
            _clientStream?.Close();
            _clientStream?.Dispose();

            _tcpClient?.Close();
            _tcpClient?.Dispose();

            _listenerStream?.Close();
            _listenerStream?.Dispose();

            _tcpListener?.Stop();
        }

        [Test, Explicit("This test takes a long time to finish")]
        public void NetworkStreamWrapper_ClosesConnection_WhenNoMessageSent()
        {
            var log = Given_Correct_Log();
            var clientSocketWrapper = Given_Correct_ClientNetworkStreamWrapper(log);
            var listenerSocketWrapper = Given_Correct_ListenerNetworkStreamWrapper(log);

            Assert.Throws<System.IO.EndOfStreamException>(() => clientSocketWrapper.ReadMessage());
        }

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
        public void ProducingWorker_KeepsConnection_WhenAuthenticationCorrect()
        { 
            var log = Given_Correct_Log();
            var clientSocketWrapper = Given_Correct_ClientNetworkStreamWrapper(log);
            var listenerSocketWrapper = Given_Correct_ListenerNetworkStreamWrapper(log);
            var producingWorker = Given_Correct_ProducingWorker(log);
            var request = new PingRequest { Message = $"{STARTED_GUID_PLACEHOLDER}_{STARTED_GUID_PLACEHOLDER}" };

            producingWorker.AddConnection(listenerSocketWrapper);

            clientSocketWrapper.WriteMessage(1, (byte)MeaRequestType.Ping, request);

            var response = clientSocketWrapper.ReadMessage();

            Assert.AreEqual("Success", ((PingRequest)response.Message).Message);
        }

        private void AssertRequestDisconnects<T>(T request, byte requestType)
        {
            var log = Given_Correct_Log();
            var clientSocketWrapper = Given_Correct_ClientNetworkStreamWrapper(log);
            var listenerSocketWrapper = Given_Correct_ListenerNetworkStreamWrapper(log);
            var producingWorker = Given_Correct_ProducingWorker(log);

            producingWorker.AddConnection(listenerSocketWrapper);

            clientSocketWrapper.WriteMessage(1, requestType, request);

            var failResponse = clientSocketWrapper.ReadMessage();

            Assert.AreEqual("Fail", ((PingRequest)failResponse.Message).Message);

            Assert.Throws<System.IO.EndOfStreamException>(() => clientSocketWrapper.ReadMessage());
        }

        private ILog Given_Correct_Log()
        {
            var logMock = new Mock<ILog>();
            return logMock.Object;
        }

        private INetworkStreamWrapper Given_Correct_ClientNetworkStreamWrapper(ILog log)
        {
            return new NetworkStreamWrapper(_clientStream, log);
        }

        private INetworkStreamWrapper Given_Correct_ListenerNetworkStreamWrapper(ILog log)
        {
            return new NetworkStreamWrapper(_listenerStream, log, true);
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

        private ProducingWorker Given_Correct_ProducingWorker(ILog log)
        {
            var messageQueueMock = new Mock<IMessageQueue>();
            var algoClientInstanceRepo = Given_Correct_AlgoClientInstanceRepository();

            messageQueueMock.Setup(m => m.Enqueue(It.IsAny<IMessageInfo>()))
                            .Callback<IMessageInfo>((request) => request.Reply(MeaResponseType.Pong, request.Message));

            return new ProducingWorker(messageQueueMock.Object, algoClientInstanceRepo, log);
        }
    }
}
