using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.Log;
using JetBrains.Annotations;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening
{
    /// <summary>
    /// Manages a set of <see cref="ConsumingWorker"/> by adding or removing instances based on the
    /// current message load
    /// </summary>
    internal class ConsumerLoadBalancer : IDisposable
    {
        private readonly List<ConsumingWorker> _workers = new List<ConsumingWorker>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly IRequestQueue _requestQueue;
        private readonly ILog _log;

        private Thread _balancingThread;
        private int _loadCounter;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a <see cref="ConsumerLoadBalancer"/> with a given <see cref="IRequestQueue"/> to process messages from
        /// </summary>
        /// <param name="requestQueue">A <see cref="IRequestQueue"/></param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestQueue"/> is null</exception>
        public ConsumerLoadBalancer(IRequestQueue requestQueue, [NotNull] ILog log)
        {
            _requestQueue = requestQueue ?? throw new ArgumentNullException(nameof(requestQueue));
            _log = log ?? throw new ArgumentNullException(nameof(log));

            _workers.Add(new ConsumingWorker(_requestQueue, _log));
            _balancingThread = new Thread(DoBalancing);
            _balancingThread.Start(_cts.Token);
        }

        /// <summary>
        /// Disposes this <see cref="ConsumerLoadBalancer"/> and all of the <see cref="ConsumingWorker"/> it manages
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Balancing thread which handles creating/removing instances of <see cref="ConsumingWorker"/>
        /// based on the average load in the past 10 seconds
        /// </summary>
        /// <param name="cancellationTokenObj">A <see cref="CancellationToken"/> used for stopping the thread</param>
        private void DoBalancing(object cancellationTokenObj)
        {
            var cancellationToken = (CancellationToken)cancellationTokenObj;

            while (!cancellationToken.IsCancellationRequested)
            {
                var avg = _workers.Average(c => c.LastMessageTime);

                var now = DateTime.Now.Ticks;

                // 1 tick: 100 ns; 500,000 ticks: 50 ms
                if (now - avg < 500_000)
                    _loadCounter++; // Use the load counter to check how many consecutive times the load has been high (or low)
                else if (now - avg > 10_000_000) // 10M ticks: 1 second
                    _loadCounter--; // Decrement if the load is low

                if (_loadCounter >= 4) // If the load is high for 2 seconds straight, spin up a new consumer
                {
                    _workers.Add(new ConsumingWorker(_requestQueue, _log));
                    _loadCounter = 0;
                }
                else if (_loadCounter <= -20 && _workers.Count > 1) // If the load has been low for 10 seconds, switch off a consumer
                {
                    var lastWorker = _workers[_workers.Count - 1];
                    lastWorker.Dispose();

                    _workers.RemoveAt(_workers.Count - 1);
                }

                Thread.Sleep(500);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                _cts.Cancel();
                _balancingThread.Join();

                foreach (var worker in _workers)
                    worker.Dispose();

                _workers.Clear();
            }

            _isDisposed = true;
        }
    }
}
