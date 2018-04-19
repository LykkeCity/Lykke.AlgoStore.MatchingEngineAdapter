using System;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening
{
    /// <summary>
    /// Used to wrap an async result to add some extra fields.
    /// This class has no use outside of the context of <see cref="ClientSocketWrapper"/>
    /// </summary>
    internal class AsyncResultWrapper : IAsyncResult
    {
        private readonly IAsyncResult _asyncResult;

        /// <summary>
        /// The wrapped <see cref="IAsyncResult"/>
        /// </summary>
        public IAsyncResult InnerAsyncResult => _asyncResult;

        public object AsyncState => _asyncResult.AsyncState;
        public WaitHandle AsyncWaitHandle => _asyncResult.AsyncWaitHandle;
        public bool CompletedSynchronously => _asyncResult.CompletedSynchronously;
        public bool IsCompleted => _asyncResult.IsCompleted;

        /// <summary>
        /// The buffer which will be filled with the message type
        /// </summary>
        public byte[] Buffer { get; set; }

        /// <summary>
        /// Initializes a <see cref="AsyncResultWrapper"/> using a given <see cref="IAsyncResult"/>
        /// </summary>
        /// <param name="innerAsyncResult">The <see cref="IAsyncResult"/> to wrap</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerAsyncResult"/> is null</exception>
        public AsyncResultWrapper(IAsyncResult innerAsyncResult)
        {
            _asyncResult = innerAsyncResult ?? throw new ArgumentNullException(nameof(innerAsyncResult));
        }
    }
}
