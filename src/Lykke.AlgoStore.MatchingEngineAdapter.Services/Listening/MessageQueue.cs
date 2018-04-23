using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening
{
    /// <summary>
    /// Represents a request queue
    /// </summary>
    public class MessageQueue : IMessageQueue
    {
        private readonly BlockingCollection<IMessageInfo> _queue = new BlockingCollection<IMessageInfo>();

        /// <summary>
        /// Queues an incoming message for processing
        /// </summary>
        /// <param name="message">The <see cref="IMessageInfo"/> to enqueue</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null</exception>
        public void Enqueue(IMessageInfo message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            _queue.Add(message);
        }

        /// <summary>
        /// Takes a message from the queue. If there are no available messages, this method will block until one is available
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to abort the operation</param>
        /// <returns>The first <see cref="IMessageInfo"/> in the queue</returns>
        public IMessageInfo Dequeue(CancellationToken cancellationToken)
        {
            return _queue.Take(cancellationToken);
        }
    }
}
