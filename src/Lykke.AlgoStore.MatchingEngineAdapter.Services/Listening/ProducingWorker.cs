using Common.Log;
using JetBrains.Annotations;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Enumerators;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Repositories;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Responses;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening
{
    /// <summary>
    /// Listens for incoming requests on a set of <see cref="IStreamWrapper"/> and
    /// handles events like network failures and invalid requests
    /// </summary>
    public class ProducingWorker : IDisposable
    {
        // This will hold all of the connected instances.
        // We use Dictionary here because there's no concurrent hashset in .NET. The value is not used
        // and will be set to 0 for every connection
        private readonly ConcurrentDictionary<string, byte> _connectionHashSet = new ConcurrentDictionary<string, byte>();

        private readonly List<IStreamWrapper> _sockets = new List<IStreamWrapper>();
        private readonly IMessageQueue _requestQueue;
        private readonly IAlgoClientInstanceRepository _algoClientInstanceRepository;
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
        /// <param name="requestQueue">The <see cref="IMessageQueue"/> to use for queueing incoming requests for processing</param>
        /// <param name="algoClientInstanceRepository">The <see cref="IAlgoClientInstanceRepository"/> to use for validating connections</param>
        /// <param name="connectionHashSet">The hashset which will hold all active connection IDs</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="requestQueue"/>, <paramref name="algoClientInstanceRepository"/>
        /// or <paramref name="log"/> are null
        /// </exception>
        public ProducingWorker(
            IMessageQueue requestQueue, 
            IAlgoClientInstanceRepository algoClientInstanceRepository, 
            ConcurrentDictionary<string, byte> connectionHashSet,
            [NotNull] ILog log)
        {
            _requestQueue = requestQueue ?? throw new ArgumentNullException(nameof(requestQueue));
            _algoClientInstanceRepository = algoClientInstanceRepository ?? throw new ArgumentNullException(nameof(algoClientInstanceRepository));
            _connectionHashSet = connectionHashSet ?? throw new ArgumentNullException(nameof(connectionHashSet));
            _log = log ?? throw new ArgumentNullException(nameof(log));

            _worker = new Thread(AcceptMessages);
            _worker.Priority = ThreadPriority.Highest;
            _worker.Start();
        }

        /// <summary>
        /// Adds a new connection to handle
        /// </summary>
        /// <param name="connection">The <see cref="IStreamWrapper"/> to handle</param>
        /// <exception cref="ObjectDisposedException">Thrown when the <see cref="ProducingWorker"/> has been disposed</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> is null</exception>
        public void AddConnection(IStreamWrapper connection)
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

                // Wait until there's a message on any of the connections.
                // ---
                // The timeout here is to take into account any new connections - if a new connection
                // is added to this worker and none of the initial ones receive any message
                // the new connection will never have its messages read.
                var index = WaitHandle.WaitAny(waitHandleArrayRef, 2_500);

                // No message received - continue to next iteration
                if (index == WaitHandle.WaitTimeout)
                    continue;

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
                    if (!RunAndCatchDisconnection(() => _sockets[index].EndReadMessage(result), out IMessageInfo request, _sockets[index]))
                        continue;

                    var isAuthenticated = _sockets[index].IsAuthenticated;

                    if (!TryAuthenticate(_sockets[index], request))
                    {
                        request.Reply((byte)MeaResponseType.Pong, new PingRequest { Message = "Fail" });
                        HandleDisconnect(_sockets[index]);
                        continue;
                    }
                    else if (isAuthenticated) // If TryAuthenticate returns true and the connection was authed before, queue message
                    {
                        // Queue the message for processing
                        _requestQueue.Enqueue(request);
                    }

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
        /// Handles the process of disconnecting and disposing a <see cref="IStreamWrapper"/>
        /// </summary>
        /// <param name="clientSocket">The <see cref="IStreamWrapper"/> to dispose</param>
        private void HandleDisconnect(IStreamWrapper clientSocket)
        {
            lock(_sync)
            {
                var index = _sockets.IndexOf(clientSocket);

                if (index < 0)
                    return;

                _sockets.RemoveAt(index);

                if (clientSocket.ID != null)
                    _connectionHashSet.TryRemove(clientSocket.ID, out byte ignore);

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
        /// <param name="connection">The <see cref="IStreamWrapper"/> to dispose in the case of failures</param>
        /// <returns>true if the delegate ran successfully, false if there was a failure</returns>
        private bool RunAndCatchDisconnection<T>(Func<T> codeToRun, out T result, IStreamWrapper connection)
        {
            try
            {
                result = codeToRun();
                return true;
            }
            catch (System.IO.InvalidDataException e)
            {
                _log.WriteWarning(nameof(ProducingWorker), nameof(RunAndCatchDisconnection), $"Client sent invalid data, dropping connection!");
            }
            catch (Exception e) when (e is System.IO.IOException || e is ObjectDisposedException)
            {
                _log.WriteInfo(nameof(ProducingWorker), nameof(RunAndCatchDisconnection), 
                    e is ObjectDisposedException ? "Connection to client was dropped" : "Connection to client was lost");
            }

            HandleDisconnect(connection);

            result = default(T);
            return false;
        } 

        private bool TryAuthenticate(IStreamWrapper connection, IMessageInfo messageInfo)
        {
            if (!connection.AuthenticationEnabled || connection.IsAuthenticated) return true;

            var pingRequest = messageInfo.Message as PingRequest;

            if (pingRequest == null)
            {
                _log.WriteWarning(nameof(ProducingWorker), nameof(TryAuthenticate),
                    $"Connection didn't send {nameof(PingRequest)} as the first message, dropping connection!");
                return false;
            }

            if(string.IsNullOrEmpty(pingRequest.Message))
            {
                _log.WriteWarning(nameof(ProducingWorker), nameof(TryAuthenticate),
                    $"Connection sent empty {nameof(PingRequest)}, dropping connection!");
                return false;
            }

            var splitString = pingRequest.Message.Split('_', StringSplitOptions.RemoveEmptyEntries);

            if (splitString.Length != 2)
            {
                _log.WriteWarning(nameof(ProducingWorker), nameof(TryAuthenticate),
                    $"Connection sent invalid format {nameof(PingRequest)}, dropping connection!");
                return false;
            }

            if (!_algoClientInstanceRepository.ExistsAlgoInstanceDataWithClientIdAsync(splitString[0], splitString[1]).Result)
            {
                _log.WriteWarning(nameof(ProducingWorker), nameof(TryAuthenticate),
                    $"Connection sent {nameof(PingRequest)} containing unknown client ID/instance ID combination, dropping connection!");
                return false;
            }

            if(_algoClientInstanceRepository
                .GetAlgoInstanceDataByClientIdAsync(splitString[0], splitString[1])
                .Result
                .AlgoInstanceStatus != AlgoInstanceStatus.Started)
            {
                _log.WriteWarning(nameof(ProducingWorker), nameof(TryAuthenticate),
                    $"Connection sent {nameof(PingRequest)} containing algo instance with status different from started, dropping connection!");
                return false;
            }

            // TryAdd is used here in place of ContainsKey, because ContainsKey introduces a security issue.
            // For example, if two new connections are sent to two workers at the same time and try authenticating with the same token,
            // both of them can pass through the ContainsKey check before either is added to the dictionary and complete authentication.
            if(!_connectionHashSet.TryAdd(pingRequest.Message, 0))
            {
                _log.WriteWarning(nameof(ProducingWorker), nameof(TryAuthenticate),
                    $"Connection sent token of an already existing connection, dropping new connection!");
                return false;
            }

            connection.MarkAuthenticated();
            connection.ID = pingRequest.Message;

            messageInfo.Reply((byte)MeaResponseType.Pong, new PingRequest { Message = "Success" });
            return true;
        }
    }
}
