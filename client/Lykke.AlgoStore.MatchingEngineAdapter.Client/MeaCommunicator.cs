using Common.Log;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    internal class MeaCommunicator : IMeaCommunicator
    {
        private readonly TcpClient _tcpClient = new TcpClient();
        private readonly IPAddress _ipAddress;
        private readonly ushort _port;
        private readonly ILog _log;

        private readonly Thread _workerThread;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private NetworkStreamWrapper _networkStreamWrapper;

        public event Action<IMessageInfo> OnMessageReceived;

        public MeaCommunicator(ILog log, IPAddress ipAddress, ushort port)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _ipAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
            _port = port;

            _log.WriteInfo(nameof(MeaCommunicator), nameof(MeaCommunicator),
                           $"Initializing MEA communicator with target address {_ipAddress}:{_port}");

            _workerThread = new Thread(AcceptMessages);
            _workerThread.Start(_cts.Token);
        }

        private void EnsureConnected()
        {
            if (_tcpClient.Connected) return;

            _log.WriteInfo(nameof(MeaCommunicator), nameof(EnsureConnected), "Connecting to MEA...");

            _tcpClient.Connect(_ipAddress, _port);
            var networkStream = _tcpClient.GetStream();
            _networkStreamWrapper = new NetworkStreamWrapper(networkStream);
        }

        private void AcceptMessages(object cancellationTokenObj)
        {
            var cancellationToken = (CancellationToken)cancellationTokenObj;

            while(!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    EnsureConnected();
                    var message = _networkStreamWrapper.ReadMessage();
                    OnMessageReceived?.Invoke(message);
                }
                catch(Exception e)
                {
                    _log.WriteError(nameof(MeaCommunicator), nameof(AcceptMessages), e);
                    Console.WriteLine(e);
                }
            }
        }

        public void SendRequest<T>(uint messageId, byte messageType, T message)
        {
            EnsureConnected();
            _networkStreamWrapper.WriteMessage(messageId, messageType, message);
        }
    }
}
