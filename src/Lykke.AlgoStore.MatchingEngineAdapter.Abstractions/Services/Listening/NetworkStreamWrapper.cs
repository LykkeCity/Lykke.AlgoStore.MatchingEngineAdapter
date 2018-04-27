using Common.Log;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening
{
    /// <summary>
    /// Wraps a <see cref="Socket"/> and provides functionality to read and send messages
    /// </summary>
    public class NetworkStreamWrapper : INetworkStreamWrapper
    {
        private static readonly Dictionary<MeaRequestType, Type> _defaultMessageTypeMap = new Dictionary<MeaRequestType, Type>
        {
            [MeaRequestType.Ping] = typeof(PingRequest),
            [MeaRequestType.MarketOrderRequest] = typeof(MarketOrderRequest)
        };

        private readonly NetworkStream _networkStream;
        private readonly Dictionary<MeaRequestType, Type> _messageTypeMap;
        private readonly ILog _log;
        private readonly Timer _authenticationTimer;

        private readonly object _sync = new object();

        private bool _isDisposed;
        private bool _isAuthenticated;

        public string ID { get; set; }

        public bool AuthenticationEnabled => _authenticationTimer != null;
        public bool IsAuthenticated => _isAuthenticated;

        /// <summary>
        /// Initializes a <see cref="NetworkStreamWrapper"/> using a given <see cref="NetworkStream"/>
        /// </summary>
        /// <param name="networkStream">The <see cref="NetworkStream"/> to wrap</param>
        /// /// <param name="log">The logger to use</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="networkStream"/> or <paramref name="log"/> is null
        /// </exception>
        public NetworkStreamWrapper(NetworkStream networkStream, ILog log) 
            : this(networkStream, log, false)
        {
        }

        /// <summary>
        /// Initializes a <see cref="NetworkStreamWrapper"/> using a given <see cref="NetworkStream"/>
        /// </summary>
        /// <param name="networkStream">The <see cref="NetworkStream"/> to wrap</param>
        /// <param name="log">The logger to use</param>
        /// <param name="useAuthentication">Whether the connection is required to be authenticated or not</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="networkStream"/> or <paramref name="log"/> is null
        /// </exception>
        public NetworkStreamWrapper(NetworkStream networkStream, ILog log, bool useAuthentication)
            : this(networkStream, log, useAuthentication, _defaultMessageTypeMap)
        {
        }

        /// <summary>
        /// Initializes a <see cref="NetworkStreamWrapper"/> using a given <see cref="NetworkStream"/> and type map
        /// </summary>
        /// <param name="networkStream">The <see cref="NetworkStream"/> to wrap</param>
        /// <param name="log">The logger to use</param>
        /// <param name="useAuthentication">Whether the connection is required to be authenticated or not</param>
        /// <param name="messageTypeMap">The type map to use when deserializing messages</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="networkStream"/> or <paramref name="log"/> is null
        /// </exception>
        public NetworkStreamWrapper(NetworkStream networkStream, ILog log, bool useAuthentication, 
            Dictionary<MeaRequestType, Type> messageTypeMap)
        {
            _networkStream = networkStream ?? throw new ArgumentNullException(nameof(networkStream));
            _log = log ?? throw new ArgumentNullException(nameof(_log));
            _messageTypeMap = messageTypeMap ?? _defaultMessageTypeMap;

            if(useAuthentication)
                _authenticationTimer = new Timer(EnsureAuthenticated, null, 10_000, Timeout.Infinite);
        }

        /// <summary>
        /// Begins an async message read
        /// </summary>
        /// <param name="callback">The callback to signal once the read is complete</param>
        /// <param name="state">User-defined state</param>
        /// <returns>A <see cref="IAsyncResult"/> containing information about the async operation</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="NetworkStreamWrapper"/> is disposed</exception>
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
        /// <returns>A <see cref="MessageInfo"/> containing information about the request and the message</returns>
        /// <exception cref="ArgumentException">Thrown when an invalid <paramref name="asyncResult"/> is given</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="NetworkStreamWrapper"/> is disposed</exception>
        public IMessageInfo EndReadMessage(IAsyncResult asyncResult)
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
        /// <returns>A <see cref="MessageInfo"/> containing information about the request and the message</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="NetworkStreamWrapper"/> is disposed</exception>
        public IMessageInfo ReadMessage()
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
        /// <param name="messageId">The ID of the request this message is a reply to</param>
        /// <param name="messageType">The message type</param>
        /// <param name="message">The message to send</param>
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="NetworkStreamWrapper"/> is disposed</exception>
        public void WriteMessage<T>(uint messageId, byte messageType, T message)
        {
            CheckDisposed();

            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, message);

                var bytes = ms.ToArray();

                lock (_sync)
                {
                    using (var bw = new BinaryWriter(_networkStream, Encoding.UTF8, true))
                    {
                        bw.Write(messageType);
                        bw.Write(messageId);
                        bw.Write((ushort)bytes.Length);
                        bw.Write(bytes);
                        bw.Flush();
                    }
                }
            }
        }

        public void MarkAuthenticated()
        {
            _isAuthenticated = true;
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
                _networkStream.Close();
                _networkStream.Dispose();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// Reads a message given a certain message type
        /// </summary>
        /// <param name="messageType">The type of the message to read</param>
        /// <returns>A <see cref="MessageInfo"/> containing information about the request and the message</returns>
        /// <exception cref="InvalidDataException">Thrown when the message type is invalid</exception>
        private MessageInfo ParseMessage(byte messageType)
        {
            if (!_messageTypeMap.ContainsKey((MeaRequestType)messageType))
                throw new InvalidDataException($"Message type {messageType} has no handler");

            var result = new MessageInfo(this);

            using (var br = new BinaryReader(_networkStream, Encoding.UTF8, true))
            {
                lock (_sync)
                {
                    result.Id = br.ReadUInt32();
                    var dataLength = br.ReadUInt16();

                    var type = _messageTypeMap[(MeaRequestType)messageType];

                    using (var ms = new MemoryStream(br.ReadBytes(dataLength)))
                    {
                        result.Message = Serializer.NonGeneric.Deserialize(type, ms);
                    }
                }
            }

            return result;
        }

        private void EnsureAuthenticated(object state)
        {
            if (_isDisposed) return;

            if (_isAuthenticated) return;

            _log.WriteWarning(nameof(NetworkStreamWrapper), nameof(EnsureAuthenticated),
                "Client failed to authenticate within the time limit, disposing connection!");
            Dispose();
        }

        private void CheckDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(NetworkStreamWrapper), "The stream wrapper has been disposed");
        }
    }
}
