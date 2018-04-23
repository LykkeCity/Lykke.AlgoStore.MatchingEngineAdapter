using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening
{
    /// <summary>
    /// Listens for incoming connections and passes them on to the load balancer
    /// </summary>
    public class ListeningService : IListeningService
    {
        private readonly IMessageQueue _requestQueue;
        private readonly IProducerLoadBalancer _producerLoadBalancer;
        private readonly ConsumerLoadBalancer _consumerLoadBalancer;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ushort _port;

        private TcpListener _listener;

        private Thread _acceptingThread;

        private bool _isDisposed;

        /// <summary>
        /// Initializes a <see cref="ListeningService"/>
        /// </summary>
        public ListeningService(IProducerLoadBalancer producerLoadBalancer, IMessageQueue requestQueue, ushort port)
        {
            _requestQueue = requestQueue ?? throw new ArgumentNullException(nameof(requestQueue));

            _producerLoadBalancer = producerLoadBalancer ?? throw new ArgumentNullException(nameof(producerLoadBalancer));
            _consumerLoadBalancer = new ConsumerLoadBalancer(_requestQueue);

            _port = port;
        }

        /// <summary>
        /// Starts listening and starts the connection accepting thread
        /// </summary>
        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            _acceptingThread = new Thread(AcceptConnections);
            _acceptingThread.Priority = ThreadPriority.Highest;
            _acceptingThread.Start(_cts.Token);
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

            if(isDisposing)
            {
                _cts.Cancel();
                _acceptingThread.Join();

                _listener.Stop();
                _producerLoadBalancer.Dispose();
                _consumerLoadBalancer.Dispose();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// Accepts a new connection until the thread is stopped
        /// </summary>
        /// <param name="cancellationTokenObj">A <see cref="CancellationToken"/></param>
        private void AcceptConnections(object cancellationTokenObj)
        {
            var cancellationToken = (CancellationToken)cancellationTokenObj;

            while(!cancellationToken.IsCancellationRequested)
            {
                var autoResetEvent = new AutoResetEvent(false);

                var callback = (AsyncCallback)((result) =>
                {
                    try
                    {
                        var socket = _listener.EndAcceptSocket(result);
                        var networkStream = new NetworkStream(socket, ownsSocket: true);
                        _producerLoadBalancer.AcceptConnection(new NetworkStreamWrapper(networkStream));
                    }
                    catch(ObjectDisposedException)
                    {
                        return;
                    }
                    finally
                    {
                        autoResetEvent.Set();
                    }
                });

                var asyncResult = _listener.BeginAcceptSocket(callback, null);

                while (!autoResetEvent.WaitOne(500) && !cancellationToken.IsCancellationRequested) ;
            }
        }
    }
}
