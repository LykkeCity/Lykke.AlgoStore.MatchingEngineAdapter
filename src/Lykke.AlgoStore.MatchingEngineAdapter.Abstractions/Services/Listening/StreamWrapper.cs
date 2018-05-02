﻿using Common.Log;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening
{
    /// <summary>
    /// Wraps a <see cref="Stream"/> and provides functionality to read and send messages
    /// </summary>
    public class StreamWrapper : IStreamWrapper
    {
        private static readonly Dictionary<byte, Type> _defaultMessageTypeMap = new Dictionary<byte, Type>
        {
            [(byte)MeaRequestType.Ping] = typeof(PingRequest),
            [(byte)MeaRequestType.MarketOrderRequest] = typeof(MarketOrderRequest)
        };

        private readonly Stream _stream;
        private readonly Dictionary<byte, Type> _messageTypeMap;
        private readonly ILog _log;
        private readonly Timer _authenticationTimer;

        private readonly object _sync = new object();

        private bool _isDisposed;
        private bool _isAuthenticated;

        public string ID { get; set; }

        public bool AuthenticationEnabled => _authenticationTimer != null;
        public bool IsAuthenticated => _isAuthenticated;

        /// <summary>
        /// Initializes a <see cref="StreamWrapper"/> using a given <see cref="Stream"/>
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to wrap</param>
        /// /// <param name="log">The logger to use</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> or <paramref name="log"/> is null
        /// </exception>
        public StreamWrapper(Stream stream, ILog log) 
            : this(stream, log, false)
        {
        }

        /// <summary>
        /// Initializes a <see cref="StreamWrapper"/> using a given <see cref="Stream"/>
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to wrap</param>
        /// <param name="log">The logger to use</param>
        /// <param name="useAuthentication">Whether the connection is required to be authenticated or not</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> or <paramref name="log"/> is null
        /// </exception>
        public StreamWrapper(Stream stream, ILog log, bool useAuthentication)
            : this(stream, log, useAuthentication, _defaultMessageTypeMap)
        {
        }

        /// <summary>
        /// Initializes a <see cref="StreamWrapper"/> using a given <see cref="Stream"/> and type map
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to wrap</param>
        /// <param name="log">The logger to use</param>
        /// <param name="useAuthentication">Whether the connection is required to be authenticated or not</param>
        /// <param name="messageTypeMap">The type map to use when deserializing messages</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> or <paramref name="log"/> is null
        /// </exception>
        public StreamWrapper(Stream stream, ILog log, bool useAuthentication, 
            Dictionary<byte, Type> messageTypeMap)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
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
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="StreamWrapper"/> is disposed</exception>
        public IAsyncResult BeginReadMessage(AsyncCallback callback, object state)
        {
            CheckDisposed();

            var buffer = new byte[1];
            var asyncResult = _stream.BeginRead(buffer, 0, buffer.Length, callback, state);

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
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="StreamWrapper"/> is disposed</exception>
        public IMessageInfo EndReadMessage(IAsyncResult asyncResult)
        {
            CheckDisposed();

            var wrapper = asyncResult as AsyncResultWrapper;

            if (wrapper == null)
                throw new ArgumentException($"{nameof(asyncResult)} is not of type {nameof(AsyncResultWrapper)}");

            _stream.EndRead(wrapper.InnerAsyncResult);
            var messageType = wrapper.Buffer[0];

            return ParseMessage(messageType);
        }

        /// <summary>
        /// Synchronous version of the <see cref="BeginReadMessage(AsyncCallback, object)"/>
        /// and <see cref="EndReadMessage(IAsyncResult)"/> methods. This method will block until a message is available.
        /// </summary>
        /// <returns>A <see cref="MessageInfo"/> containing information about the request and the message</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="StreamWrapper"/> is disposed</exception>
        public IMessageInfo ReadMessage()
        {
            CheckDisposed();

            var buffer = new byte[1];
            _stream.Read(buffer, 0, buffer.Length);

            return ParseMessage(buffer[0]);
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        /// <typeparam name="T">The type of the message</typeparam>
        /// <param name="messageId">The ID of the request this message is a reply to</param>
        /// <param name="messageType">The message type</param>
        /// <param name="message">The message to send</param>
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="StreamWrapper"/> is disposed</exception>
        public void WriteMessage<T>(uint messageId, byte messageType, T message)
        {
            CheckDisposed();

            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, message);

                var bytes = ms.ToArray();

                lock (_sync)
                {
                    using (var bw = new BinaryWriter(_stream, Encoding.UTF8, true))
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
                _stream.Close();
                _stream.Dispose();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// Reads a message given a certain message type
        /// </summary>
        /// <param name="messageType">The type of the message to read</param>
        /// <returns>A <see cref="MessageInfo"/> containing information about the request and the message</returns>
        /// <exception cref="InvalidDataException">Thrown when the message type or payload is invalid</exception>
        private MessageInfo ParseMessage(byte messageType)
        {
            if (!_messageTypeMap.ContainsKey(messageType))
                throw new InvalidDataException($"Message type {messageType} has no handler");

            var result = new MessageInfo(this);

            using (var br = new BinaryReader(_stream, Encoding.UTF8, true))
            {
                lock (_sync)
                {
                    result.Id = br.ReadUInt32();
                    var dataLength = br.ReadUInt16();

                    var type = _messageTypeMap[messageType];

                    using (var ms = new MemoryStream(br.ReadBytes(dataLength)))
                    {
                        try
                        {
                            result.Message = Serializer.NonGeneric.Deserialize(type, ms);
                        }
                        catch(Exception e)
                        {
                            // Rethrow wrapped exception here to prevent having to catch specific library exceptions
                            throw new InvalidDataException("Error deserializing message payload. See inner exception for details",
                                                           e);
                        }
                    }
                }
            }

            return result;
        }

        private void EnsureAuthenticated(object state)
        {
            if (_isDisposed) return;

            if (_isAuthenticated) return;

            _log.WriteWarning(nameof(StreamWrapper), nameof(EnsureAuthenticated),
                "Client failed to authenticate within the time limit, disposing connection!");
            Dispose();
        }

        private void CheckDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(StreamWrapper), "The stream wrapper has been disposed");
        }
    }
}