using Common.Log;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Responses;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    internal class MeaCommunicator : IMeaCommunicator
    {
        private static readonly Dictionary<byte, Type> _defaultMessageTypeMap = new Dictionary<byte, Type>
        {
            [(byte)MeaResponseType.Pong] = typeof(PingRequest),
            [(byte)MeaResponseType.MarketOrderResponse] = typeof(ResponseModel<double>)
        };

        private readonly TcpClient _tcpClient = new TcpClient();
        private readonly IPAddress _ipAddress;
        private readonly ushort _port;
        private readonly ILog _log;

        private Thread _workerThread;
        private CancellationTokenSource _cts;
        private NetworkStreamWrapper _networkStreamWrapper;

        public event Action OnConnectionEstablished;
        public event Action<IMessageInfo> OnMessageReceived;

        public MeaCommunicator(ILog log, IPAddress ipAddress, ushort port)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _ipAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
            _port = port;

            _log.WriteInfo(nameof(MeaCommunicator), nameof(MeaCommunicator),
                           $"Initializing MEA communicator with target address {_ipAddress}:{_port}");
        }

        public void Start()
        {
            if (_workerThread != null) return;

            _cts = new CancellationTokenSource();

            _workerThread = new Thread(AcceptMessages);
            _workerThread.Start(_cts.Token);
        }

        public void SendRequest<T>(uint messageId, byte messageType, T message)
        {
            EnsureConnected();
            _networkStreamWrapper.WriteMessage(messageId, messageType, message);
        }

        private void EnsureConnected()
        {
            if (_tcpClient.Connected) return;

            _log.WriteInfo(nameof(MeaCommunicator), nameof(EnsureConnected), "Connecting to MEA...");

            _tcpClient.Connect(_ipAddress, _port);
            var networkStream = _tcpClient.GetStream();
            _networkStreamWrapper = new NetworkStreamWrapper(networkStream, _log, false, _defaultMessageTypeMap);

            OnConnectionEstablished?.Invoke();
        }

        private void AcceptMessages(object cancellationTokenObj)
        {
            var cancellationToken = (CancellationToken)cancellationTokenObj;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    EnsureConnected();
                    var message = _networkStreamWrapper.ReadMessage();
                    OnMessageReceived?.Invoke(message);
                }
                catch (Exception e)
                {
                    _log.WriteError(nameof(MeaCommunicator), nameof(AcceptMessages), e);
                    Console.WriteLine(e);
                }
            }
        }
    }
}
