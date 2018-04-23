using Common.Log;
using JetBrains.Annotations;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Responses;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening
{
    /// <summary>
    /// Processes queued messages and sends appropriate replies
    /// </summary>
    public class ConsumingWorker : IDisposable
    {
        private readonly Dictionary<Type, Action<IMessageInfo>> _messageHandlers = new Dictionary<Type, Action<IMessageInfo>>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly IMessageQueue _requestQueue;
        private readonly ILog _log;

        private Thread _thread;

        private bool _isDisposed;

        private long _lastMessageTime = DateTime.Now.Ticks;

        /// <summary>
        /// A timestamp of the last time a message was processed
        /// </summary>
        public long LastMessageTime => Interlocked.CompareExchange(ref _lastMessageTime, 0, 0);

        /// <summary>
        /// Initializes a <see cref="ConsumingWorker"/> using a given <see cref="IMessageQueue"/> to process messages from
        /// </summary>
        /// <param name="requestQueue">A <see cref="IMessageQueue"/></param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestQueue"/> is null</exception>
        public ConsumingWorker(IMessageQueue requestQueue, [NotNull] ILog log)
        {
            _requestQueue = requestQueue ?? throw new ArgumentNullException();

            _messageHandlers.Add(typeof(PingRequest), PingHandler);

            _thread = new Thread(DoWork) { Priority = ThreadPriority.Highest };
            _thread.Start(_cts.Token);

            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Disposes the <see cref="ConsumingWorker"/>
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
                _cts.Cancel();
                _thread.Interrupt();
                _thread.Join();
            }

            _isDisposed = true;
        }

        private void DoWork(object cancellationTokenObj)
        {
            var cancellationToken = (CancellationToken)cancellationTokenObj;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var request = _requestQueue.Dequeue(cancellationToken);
                    Interlocked.Exchange(ref _lastMessageTime, DateTime.Now.Ticks);

                    var messageType = request.Message.GetType();

                    if (_messageHandlers.ContainsKey(messageType))
                        _messageHandlers[messageType](request);
                }
                catch (OperationCanceledException exception)
                {
                    _log.WriteErrorAsync(nameof(ConsumingWorker), nameof(DoWork), null, exception).Wait();
                    return;
                }
            }
        }

        /// <summary>
        /// Handles a <see cref="PingMessage"/> by replying with <see cref="Responses.MeaResponseType.Pong"/> containing
        /// the same message
        /// </summary>
        /// <param name="request">The <see cref="MessageInfo"/> containing the message</param>
        private void PingHandler(IMessageInfo request)
        {
            var msg = (PingRequest)request.Message;

            request.Reply(MeaResponseType.Pong, msg);
        }
    }
}
