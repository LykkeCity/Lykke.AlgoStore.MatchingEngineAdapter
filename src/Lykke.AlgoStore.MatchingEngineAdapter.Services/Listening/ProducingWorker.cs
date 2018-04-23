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
    /// Listens for incoming requests on a set of <see cref="IClientSocketWrapper"/> and
    /// handles events like network failures and invalid requests
    /// </summary>
    internal class ProducingWorker : IDisposable
    {
        private readonly List<IClientSocketWrapper> _sockets = new List<IClientSocketWrapper>();
        private readonly IRequestQueue _requestQueue;
        private readonly Thread _worker;
        private readonly object _sync = new object();
        private readonly ManualResetEvent _hasConnectionsEvent = new ManualResetEvent(false);

        private readonly ILog _log;


        private IAsyncResult[] _readArray;
        private WaitHandle[] _waitHandleArray;

        private bool _isDisposed;

        /// <summary>
        /// Returns the number of active connections this <see cref="ProducingWorker"/> is handling
        /// </summary>
        public int ConnectionCount => GetConnectionCount();

        /// <summary>
        /// Initializes a new instance of <see cref="ProducingWorker"/>
        /// </summary>
        /// <param name="requestQueue">The <see cref="IRequestQueue"/> to use for queueing incoming requests for processing</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestQueue"/> is null</exception>
        public ProducingWorker(IRequestQueue requestQueue, [NotNull] ILog log)
        {
            _requestQueue = requestQueue ?? throw new ArgumentNullException(nameof(requestQueue));
            _log = log ?? throw new ArgumentNullException(nameof(log));

            _worker = new Thread(AcceptMessages);
            _worker.Priority = ThreadPriority.Highest;
            _worker.Start();
        }

        /// <summary>
        /// Adds a new connection to handle
        /// </summary>
        /// <param name="connection">The <see cref="IClientSocketWrapper"/> to handle</param>
        /// <exception cref="ObjectDisposedException">Thrown when the <see cref="ProducingWorker"/> has been disposed</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> is null</exception>
        public void AddConnection(IClientSocketWrapper connection)
        {
            if (_isDisposed)
                throw new ObjectDisposedException("The worker has been disposed");

            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            lock (_sync)
            {
                _sockets.Add(connection);

                if(_readArray != null)
                {
                    // Append the new connection async result and wait handle to the arrays
                    var newReadArray = new IAsyncResult[_readArray.Length + 1];
                    var newWaitHandleArray = new WaitHandle[_waitHandleArray.Length + 1];

                    Array.Copy(_readArray, newReadArray, _readArray.Length);
                    Array.Copy(_waitHandleArray, newWaitHandleArray, _waitHandleArray.Length);

                    IAsyncResult asyncResult;

                    if(!RunAndCatchDisconnection(() => connection.BeginReadMessage(null, null), out asyncResult, connection))
                        return;

                    newReadArray[_readArray.Length] = asyncResult;
                    newWaitHandleArray[_waitHandleArray.Length] = asyncResult.AsyncWaitHandle;

                    _readArray = newReadArray;
                    _waitHandleArray = newWaitHandleArray;

                    return;
                }
            }

            if (!RunAndCatchDisconnection(() => _sockets.Select(s => s.BeginReadMessage(null, null)).ToArray(), out _readArray, connection))
                return;

            _waitHandleArray = _readArray.Select(ar => ar.AsyncWaitHandle).ToArray();

            _hasConnectionsEvent.Set();
        }

        /// <summary>
        /// Disposes this <see cref="ProducingWorker"/> and closes all of the connections it handles
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
                foreach (var socket in _sockets)
                    socket.Dispose();

                _worker.Interrupt();
                _worker.Join();

                _sockets.Clear();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// Worker thread which handles incoming messages
        /// </summary>
        private void AcceptMessages()
        {
            while (true)
            {
                try
                {
                    // Wait until we have connections
                    _hasConnectionsEvent.WaitOne();
                }
                catch(ThreadInterruptedException exception)
                {
                    _log.WriteErrorAsync(nameof(ProducingWorker), nameof(AcceptMessages), null, exception).Wait();
                    return;
                }

                IAsyncResult[] readArrayRef;
                WaitHandle[] waitHandleArrayRef;

                lock (_sync)
                {
                    // Grab references here in case of a new connection changing the arrays
                    readArrayRef = _readArray;
                    waitHandleArrayRef = _waitHandleArray;
                }

                // Wait until there's a message on any of the connections
                var index = WaitHandle.WaitAny(waitHandleArrayRef);

                var result = readArrayRef[index];

                lock(_sync)
                {
                    if (readArrayRef != _readArray) // Arrays are different, figure out where our result went
                    {
                        index = Array.IndexOf(_readArray, result);

                        readArrayRef = _readArray;
                        waitHandleArrayRef = _waitHandleArray;
                    }

                    // Finish reading the message
                    if (!RunAndCatchDisconnection(() => _sockets[index].EndReadMessage(result), out IRequestInfo request, _sockets[index]))
                        continue;

                    // Queue the message for processing
                    _requestQueue.Enqueue(request);

                    // Start waiting for a new message
                    if (!RunAndCatchDisconnection(() => _sockets[index].BeginReadMessage(null, null), out IAsyncResult newResult, _sockets[index]))
                        continue;

                    readArrayRef[index] = newResult;
                    waitHandleArrayRef[index] = readArrayRef[index].AsyncWaitHandle;
                }
            }
        }

        private int GetConnectionCount()
        {
            lock(_sync)
            {
                return _sockets.Count;
            }
        }

        /// <summary>
        /// Handles the process of disconnecting and disposing a <see cref="IClientSocketWrapper"/>
        /// </summary>
        /// <param name="clientSocket">The <see cref="IClientSocketWrapper"/> to dispose</param>
        private void HandleDisconnect(IClientSocketWrapper clientSocket)
        {
            lock(_sync)
            {
                var index = _sockets.IndexOf(clientSocket);

                if (index < 0)
                    return;

                _sockets.RemoveAt(index);

                clientSocket.Dispose();

                if (_readArray == null) return;

                // We need to remove the connection from the arrays
                var newReadArray = new IAsyncResult[_readArray.Length - 1];
                var newWaitHandleArray = new WaitHandle[_waitHandleArray.Length - 1];

                // We need to preserve the async results/wait handles, so copy them to the new array
                Array.Copy(_readArray, 0, newReadArray, 0, index);
                Array.Copy(_readArray, index + 1, newReadArray, index, _readArray.Length - index - 1);
                _readArray = newReadArray;

                Array.Copy(_waitHandleArray, 0, newWaitHandleArray, 0, index);
                Array.Copy(_waitHandleArray, index + 1, newWaitHandleArray, index, _waitHandleArray.Length - index - 1);
                _waitHandleArray = newWaitHandleArray;

                if (_sockets.Count == 0)
                    _hasConnectionsEvent.Reset();
            }
        }

        /// <summary>
        /// Runs a delegate and catches any events like connection failure and invalid requests
        /// </summary>
        /// <typeparam name="T">The type the delegate will return</typeparam>
        /// <param name="codeToRun">The delegate to run and check for failures</param>
        /// <param name="result">The result to set to the return value of the delegate</param>
        /// <param name="connection">The <see cref="IClientSocketWrapper"/> to dispose in the case of failures</param>
        /// <returns>true if the delegate ran successfully, false if there was a failure</returns>
        private bool RunAndCatchDisconnection<T>(Func<T> codeToRun, out T result, IClientSocketWrapper connection)
        {
            try
            {
                result = codeToRun();
                return true;
            }
            catch (System.IO.InvalidDataException e)
            {
                _log.WriteErrorAsync(nameof(ProducingWorker), nameof(RunAndCatchDisconnection), null, e).Wait();
                Console.WriteLine($"Client sent invalid data, dropping connection: {e}");
            }
            catch (Exception e) when (e is System.IO.IOException || e is ObjectDisposedException)
            {
                _log.WriteErrorAsync(nameof(ProducingWorker), nameof(RunAndCatchDisconnection), null, e).Wait();
                Console.WriteLine($"Connection to client was lost: {e}");
            }

            HandleDisconnect(connection);

            result = default(T);
            return false;
        } 
    }
}
