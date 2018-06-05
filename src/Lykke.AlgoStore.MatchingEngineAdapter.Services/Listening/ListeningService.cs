using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
using System;
using System.Net;
using System.Net.Sockets;
using Common.Log;
using JetBrains.Annotations;
using System.Threading.Tasks;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using System.Collections.Concurrent;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Repositories;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening
{
    /// <summary>
    /// Listens for incoming connections and passes them on to the load balancer
    /// </summary>
    public class ListeningService : IListeningService
    {
        // This will hold all of the connected instances.
        // We use Dictionary here because there's no concurrent hashset in .NET. The value is not used
        // and will be set to 0 for every connection
        private readonly ConcurrentDictionary<string, byte> _connectionHashSet = new ConcurrentDictionary<string, byte>();
        private readonly ConcurrentDictionary<Task, byte> _allWorkers = new ConcurrentDictionary<Task, byte>();

        private readonly IAlgoClientInstanceRepository _clientInstanceRepository;
        private readonly IMessageHandler _messageHandler;
        private readonly ILog _log;
        private readonly ushort _port;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private TcpListener _listener;

        private bool _isDisposed;

        /// <summary>
        /// Initializes a <see cref="ListeningService"/>
        /// </summary>
        public ListeningService(
            IAlgoClientInstanceRepository clientInstanceRepository,
            IMessageHandler messageHandler,
            [NotNull] ILog log,
            ushort port)
        {
            _clientInstanceRepository = clientInstanceRepository ?? throw new ArgumentNullException(nameof(clientInstanceRepository));
            _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
            _log = log ?? throw new ArgumentNullException(nameof(log));

            _port = port;
        }

        /// <summary>
        /// Starts listening and starts the connection accepting thread
        /// </summary>
        public async Task Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start();

            await AcceptConnections();
        }

        /// <summary>
        /// Disposes this <see cref="ListeningService"/> by shutting down the accepting thread,
        /// the listener and disposing the load balancers
        /// </summary>
        public void Stop()
        {
            Dispose();
        }

        /// <summary>
        /// Disposes this <see cref="ListeningService"/> by shutting down the accepting thread,
        /// the listener and disposing the load balancers
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed) return;

            if (isDisposing)
            {
                _listener.Stop();
                _listener.Server.Close();
                _listener.Server.Dispose();

                var allConnections = _allWorkers.Keys.ToArray();

                _cts.Cancel();

                var allConnectionsClosedTask = Task.WhenAll(allConnections);
                allConnectionsClosedTask.Wait();

                _cts.Dispose();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// Accepts new connections until the listener is stopped
        /// </summary>
        private async Task AcceptConnections()
        {
            while (true)
            {
                Socket socket;

                try
                {
                    socket = await _listener.AcceptSocketAsync();
                }
                catch(ObjectDisposedException) // The listening service has been disposed
                {
                    return;
                }

                await _log.WriteInfoAsync(nameof(ListeningService), nameof(AcceptConnections),
                    $"Accepting incoming connection from {socket.RemoteEndPoint}");

                var networkStream = new NetworkStream(socket, true);
                var streamWrapper = new StreamWrapper(networkStream, _log, socket.RemoteEndPoint, true);

                var connectionWorker = new ConnectionWorker(streamWrapper, 
                                _messageHandler, _clientInstanceRepository, _log, CheckConnectionExists);

                var workerTask = connectionWorker.AcceptMessagesAsync(_cts.Token);

                // Intentionally disabled unawaited task warning here
#pragma warning disable 4014
                workerTask.ContinueWith((task) =>
                {
                    if(task.IsFaulted)
                    {
                        _log.WriteError(nameof(ListeningService), nameof(AcceptConnections),
                            task.Exception);
                    }

                    _allWorkers.TryRemove(task, out byte unused);
                    if(!string.IsNullOrEmpty(streamWrapper.ID))
                        _connectionHashSet.Remove(streamWrapper.ID, out unused);
                });
#pragma warning restore 4014

                _allWorkers.TryAdd(workerTask, 0);
            }
        }

        private async Task<bool> CheckConnectionExists(string id)
        {
            if (!_connectionHashSet.TryAdd(id, 0))
            {
                await _log.WriteWarningAsync(nameof(ConnectionWorker), nameof(CheckConnectionExists),
                    $"Connection sent token of an already existing connection, dropping new connection!");

                return false;
            }

            return true;
        }
    }
}
