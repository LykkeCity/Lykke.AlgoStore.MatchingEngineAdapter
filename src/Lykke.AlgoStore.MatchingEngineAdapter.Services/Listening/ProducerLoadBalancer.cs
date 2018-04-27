using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using System;
using System.Collections.Generic;
using Common.Log;
using JetBrains.Annotations;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Repositories;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using System.Collections.Concurrent;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening
{
    /// <summary>
    /// Manages the incoming connections by balancing them between <see cref="ProducingWorker"/>
    /// and automatically manages the <see cref="ProducingWorker"/> count based on the number of connections
    /// </summary>
    public class ProducerLoadBalancer : IProducerLoadBalancer
    {
        /// <summary>
        /// The maximum amount of connections a worker can handle
        /// </summary>
        private const int MAX_CONNECTIONS_PER_WORKER = 100;

        private readonly ConcurrentDictionary<string, byte> _activeConnections = new ConcurrentDictionary<string, byte>();
        private readonly List<ProducingWorker> _workers = new List<ProducingWorker>();
        private readonly IMessageQueue _requestQueue;
        private readonly IAlgoClientInstanceRepository _algoClientInstanceRepository;
        private readonly ILog _log;

        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of <see cref="ProducerLoadBalancer"/>
        /// </summary>
        /// <param name="requestQueue">The <see cref="IMessageQueue"/> to use for queueing incoming requests for processing</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="requestQueue"/>, <paramref name="algoClientInstanceRepository"/>
        /// or <paramref name="log"/> are null
        /// </exception>
        public ProducerLoadBalancer(
            IMessageQueue requestQueue,
            IAlgoClientInstanceRepository algoClientInstanceRepository,
            [NotNull] ILog log)
        {
            _requestQueue = requestQueue ?? throw new ArgumentNullException(nameof(requestQueue));
            _algoClientInstanceRepository = algoClientInstanceRepository ?? throw new ArgumentNullException(nameof(algoClientInstanceRepository));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Accepts an incoming connection and sends it to an appropriate <see cref="ProducingWorker"/>
        /// </summary>
        /// <param name="connection">The connection to accept</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> is null</exception>
        public void AcceptConnection(INetworkStreamWrapper connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            _log.WriteInfoAsync(nameof(ProducerLoadBalancer), nameof(AcceptConnection), null, "Accept connection").Wait();

            // Find out which worker has the least connections
            var minConnections = MAX_CONNECTIONS_PER_WORKER;
            ProducingWorker leastLoadWorker = null;

            for (int i = 0; i < _workers.Count; i++)
            {
                var worker = _workers[i];

                if (worker.ConnectionCount < minConnections)
                {
                    leastLoadWorker = worker;
                    minConnections = worker.ConnectionCount;
                }
                else if (worker.ConnectionCount == 0) // More than one worker with no connections, shut it down
                {
                    worker.Dispose();
                    _workers.RemoveAt(i);
                    i--;
                }
            }

            if (minConnections == MAX_CONNECTIONS_PER_WORKER)
            {
                // All workers are on max load, spin up a new one
                var newWorker = new ProducingWorker(_requestQueue, _algoClientInstanceRepository, _activeConnections, _log);
                newWorker.AddConnection(connection);

                _workers.Add(newWorker);
            }
            else
            {
                leastLoadWorker.AddConnection(connection);
            }
        }

        /// <summary>
        /// Disposes this <see cref="ProducerLoadBalancer"/> and all of the <see cref="ProducingWorker"/> it manages
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
                foreach (var worker in _workers)
                    worker.Dispose();

                _workers.Clear();
            }

            _isDisposed = true;
        }
    }
}
