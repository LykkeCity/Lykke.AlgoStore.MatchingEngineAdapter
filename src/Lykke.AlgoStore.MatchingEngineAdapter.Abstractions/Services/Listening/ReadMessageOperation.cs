using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening
{
    /// <summary>
    /// Used to encapsulate reading an MEA message from a stream as one operation.
    /// </summary>
    internal class ReadMessageOperation : IAsyncResult
    {
        private readonly Stream _stream;
        private readonly Func<byte, Stream, IMessageInfo> _messageParser;

        private readonly object _state;
        private readonly AsyncCallback _callback;

        private readonly ManualResetEvent _waitHandle = new ManualResetEvent(false);
        private readonly IAsyncResult _streamReadAsyncresult;

        private IMessageInfo _operationResult;
        private Exception _operationException;

        public object AsyncState => _state;
        public WaitHandle AsyncWaitHandle => _waitHandle;
        public bool CompletedSynchronously => _streamReadAsyncresult.CompletedSynchronously;
        public bool IsCompleted => _waitHandle.WaitOne(0);

        /// <summary>
        /// Blocks until the result is available and returns the result if the operation completed successfully,
        /// throws the encountered exception otherwise
        /// </summary>
        public IMessageInfo Result => GetResult();

        /// <summary>
        /// Initializes a <see cref="ReadMessageOperation"/> and starts reading a message from a given <see cref="Stream"/>
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to read a message from</param>
        /// <param name="messageParser">This will be called to parse the given message type</param>
        /// <param name="callback">Optional, will be called once the operation is complete</param>
        /// <param name="state">Optional, holds user information to identify the <see cref="IAsyncResult"/></param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> or <paramref name="messageParser"/> is null
        /// </exception>
        public ReadMessageOperation(
            Stream stream,
            Func<byte, Stream, IMessageInfo> messageParser,
            AsyncCallback callback, 
            object state)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _messageParser = messageParser ?? throw new ArgumentNullException(nameof(messageParser));
            _callback = callback;
            _state = state;

            var buffer = new byte[1];
            _streamReadAsyncresult = _stream.BeginRead(buffer, 0, buffer.Length, OnOperationCompleted, buffer);
        }

        private void OnOperationCompleted(IAsyncResult asyncResult)
        {
            try
            {
                _stream.EndRead(asyncResult);

                var buffer = asyncResult.AsyncState as byte[];
                _operationResult = _messageParser(buffer[0], _stream);
            }
            catch(Exception e)
            {
                _operationException = e;
            }
            finally
            {
                _waitHandle.Set();
                _callback?.Invoke(asyncResult);
            }
        }

        private IMessageInfo GetResult()
        {
            if (!IsCompleted)
                _waitHandle.WaitOne();

            if (_operationException != null) // ExceptionDispatchInfo preserves the original stack trace of the exception
                ExceptionDispatchInfo.Capture(_operationException).Throw();

            return _operationResult;
        }
    }
}
