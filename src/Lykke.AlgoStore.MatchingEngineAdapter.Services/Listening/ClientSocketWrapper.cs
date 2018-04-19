using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Responses;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening
{
    /// <summary>
    /// Wraps a <see cref="Socket"/> and provides functionality to read and send messages
    /// </summary>
    internal class ClientSocketWrapper : IClientSocketWrapper
    {
        private static readonly Dictionary<MeaRequestType, Type> _messageTypeMap = new Dictionary<MeaRequestType, Type>
        {
            [MeaRequestType.Ping] = typeof(PingRequest)
        };

        private readonly Socket _socket;
        private readonly NetworkStream _networkStream;

        private bool _isDisposed;

        /// <summary>
        /// Initializes a <see cref="ClientSocketWrapper"/> using a given <see cref="Socket"/>
        /// </summary>
        /// <param name="socket">The <see cref="Socket"/> to wrap</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="socket"/> is null</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="socket"/> is not connected</exception>
        public ClientSocketWrapper(Socket socket)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));

            if (!socket.Connected)
                throw new ArgumentException($"{nameof(socket)} must be a connected socket");

            _socket = socket;
            _networkStream = new NetworkStream(_socket, true);
        }

        /// <summary>
        /// Begins an async message read
        /// </summary>
        /// <param name="callback">The callback to signal once the read is complete</param>
        /// <param name="state">User-defined state</param>
        /// <returns>A <see cref="IAsyncResult"/> containing information about the async operation</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="ClientSocketWrapper"/> is disposed</exception>
        public IAsyncResult BeginReadMessage(AsyncCallback callback, object state)
        {
            CheckDisposed();

            var buffer = new byte[1];
            var asyncResult = _networkStream.BeginRead(buffer, 0, buffer.Length, callback, state);

            return new AsyncResultWrapper(asyncResult)
            {
                Buffer = buffer
            };
        }

        /// <summary>
        /// Finishes an async message read. This method blocks if the corresponding 
        /// <see cref="BeginReadMessage(AsyncCallback, object)"/> has not yet finished
        /// </summary>
        /// <param name="asyncResult">
        /// The <see cref="IAsyncResult"/> returned by the corresponding <see cref="BeginReadMessage(AsyncCallback, object)"/>
        /// </param>
        /// <returns>A <see cref="RequestInfo"/> containing information about the request and the message</returns>
        /// <exception cref="ArgumentException">Thrown when an invalid <paramref name="asyncResult"/> is given</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="ClientSocketWrapper"/> is disposed</exception>
        public IRequestInfo EndReadMessage(IAsyncResult asyncResult)
        {
            CheckDisposed();

            var wrapper = asyncResult as AsyncResultWrapper;

            if (wrapper == null)
                throw new ArgumentException($"{nameof(asyncResult)} is not of type {nameof(AsyncResultWrapper)}");

            _networkStream.EndRead(wrapper.InnerAsyncResult);
            var messageType = wrapper.Buffer[0];

            return ParseMessage(messageType);
        }

        /// <summary>
        /// Synchronous version of the <see cref="BeginReadMessage(AsyncCallback, object)"/>
        /// and <see cref="EndReadMessage(IAsyncResult)"/> methods. This method will block until a message is available.
        /// </summary>
        /// <returns>A <see cref="RequestInfo"/> containing information about the request and the message</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="ClientSocketWrapper"/> is disposed</exception>
        public IRequestInfo ReadMessage()
        {
            CheckDisposed();

            var buffer = new byte[1];
            _networkStream.Read(buffer, 0, buffer.Length);

            return ParseMessage(buffer[0]);
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        /// <typeparam name="T">The type of the message</typeparam>
        /// <param name="requestId">The ID of the request this message is a reply to</param>
        /// <param name="responseType">The response type</param>
        /// <param name="message">The message to send</param>
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="ClientSocketWrapper"/> is disposed</exception>
        public void WriteMessage<T>(uint requestId, MeaResponseType responseType, T message)
        {
            CheckDisposed();

            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, message);

                var bytes = ms.ToArray();

                using (var bw = new BinaryWriter(_networkStream, Encoding.UTF8, true))
                {
                    bw.Write((byte)responseType);
                    bw.Write(requestId);
                    bw.Write((ushort)bytes.Length);
                    bw.Write(bytes);
                }
            }
        }

        /// <summary>
        /// Disposes the wrapper and closes the underlying connection
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
                _networkStream.Dispose();
                _socket.Close();
                _socket.Dispose();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// Reads a message given a certain message type
        /// </summary>
        /// <param name="messageType">The type of the message to read</param>
        /// <returns>A <see cref="RequestInfo"/> containing information about the request and the message</returns>
        /// <exception cref="InvalidDataException">Thrown when the message type is invalid</exception>
        private RequestInfo ParseMessage(byte messageType)
        {
            if (!Enum.IsDefined(typeof(MeaRequestType), (int)messageType))
                throw new InvalidDataException($"Message type {messageType} is not defined");

            var result = new RequestInfo(this);

            using (var br = new BinaryReader(_networkStream, Encoding.UTF8, true))
            {
                result.Id = br.ReadUInt32();
                var dataLength = br.ReadUInt16();

                var type = _messageTypeMap[(MeaRequestType)messageType];

                using (var ms = new MemoryStream(br.ReadBytes(dataLength)))
                {
                    result.Message = Serializer.NonGeneric.Deserialize(type, ms);
                }
            }

            return result;
        }

        private void CheckDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException("The socket wrapper has been disposed");
        }
    }
}
