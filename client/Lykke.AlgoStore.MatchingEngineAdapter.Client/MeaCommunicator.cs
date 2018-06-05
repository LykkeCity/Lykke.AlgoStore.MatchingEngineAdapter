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
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    internal class MeaCommunicator : IMeaCommunicator
    {
        private static readonly Dictionary<byte, Type> _defaultMessageTypeMap = new Dictionary<byte, Type>
        {
            [(byte)MeaResponseType.Pong] = typeof(PingRequest),
            [(byte)MeaResponseType.MarketOrderResponse] = typeof(ResponseModel<double>)
        };

        private TcpClient _tcpClient = new TcpClient();
        private readonly IPAddress _ipAddress;
        private readonly ushort _port;
        private readonly ILog _log;

        private Task _workerTask;
        private CancellationTokenSource _cts;
        private IStreamWrapper _streamWrapper;
        private bool _isDisposed;

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
            if (_workerTask != null) return;

            _cts = new CancellationTokenSource();

            _workerTask = AcceptMessages(_cts.Token);
        }

        public async Task Stop()
        {
            if (_workerTask == null) return;

            _cts.Cancel();
            _tcpClient.Dispose();
            _streamWrapper.Dispose();

            await _workerTask;

            _cts.Dispose();
            _workerTask = null;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public async Task SendRequestAsync<T>(uint messageId, byte messageType, T message)
        {
            EnsureConnected();
            await _streamWrapper.WriteMessageAsync(messageId, messageType, message);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (!disposing) return;

            Stop().Wait();

            _isDisposed = true;
        }

        private void EnsureConnected()
        {
            if (_tcpClient.Connected) return;

            _log.WriteInfo(nameof(MeaCommunicator), nameof(EnsureConnected), "Connecting to MEA...");

            _tcpClient?.Dispose();

            _tcpClient = new TcpClient();
            _tcpClient.Connect(_ipAddress, _port);

            var networkStream = _tcpClient.GetStream();
            _streamWrapper = new StreamWrapper(networkStream, _log, new IPEndPoint(_ipAddress, _port),
                                               false, _defaultMessageTypeMap);

            OnConnectionEstablished?.Invoke();
        }

        private async Task AcceptMessages(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    EnsureConnected();
                    var message = await _streamWrapper.ReadMessageAsync();
                    OnMessageReceived?.Invoke(message);
                }
                catch (Exception e)
                {
                    if(!cancellationToken.IsCancellationRequested)
                        await _log.WriteErrorAsync(nameof(MeaCommunicator), nameof(AcceptMessages), e);
                }
            }
        }
    }
}
