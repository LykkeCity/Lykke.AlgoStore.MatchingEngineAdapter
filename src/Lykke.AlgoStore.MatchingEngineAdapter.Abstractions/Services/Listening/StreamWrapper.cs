using Common.Log;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly EndPoint _remoteEndPoint;
        private readonly CancellationTokenSource _cts;

        private bool _isDisposed;

        public string ID { get; set; }

        public bool AuthenticationEnabled { get; private set; }
        public bool IsAuthenticated { get; private set; }

        /// <summary>
        /// Initializes a <see cref="StreamWrapper"/> using a given <see cref="Stream"/>
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to wrap</param>
        /// <param name="log">The logger to use</param>
        /// <param name="remoteEndPoint">The remote address of the connection</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> or <paramref name="log"/> is null
        /// </exception>
        public StreamWrapper(Stream stream, ILog log, EndPoint remoteEndPoint) 
            : this(stream, log, remoteEndPoint, false)
        {
        }

        /// <summary>
        /// Initializes a <see cref="StreamWrapper"/> using a given <see cref="Stream"/>
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to wrap</param>
        /// <param name="log">The logger to use</param>
        /// <param name="remoteEndPoint">The remote address of the connection</param>
        /// <param name="useAuthentication">Whether the connection is required to be authenticated or not</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> or <paramref name="log"/> is null
        /// </exception>
        public StreamWrapper(Stream stream, ILog log, EndPoint remoteEndPoint, bool useAuthentication)
            : this(stream, log, remoteEndPoint, useAuthentication, _defaultMessageTypeMap)
        {
        }

        /// <summary>
        /// Initializes a <see cref="StreamWrapper"/> using a given <see cref="Stream"/> and type map
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to wrap</param>
        /// <param name="log">The logger to use</param>
        /// <param name="remoteEndPoint">The remote address of the connection</param>
        /// <param name="useAuthentication">Whether the connection is required to be authenticated or not</param>
        /// <param name="messageTypeMap">The type map to use when deserializing messages</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> or <paramref name="log"/> is null
        /// </exception>
        public StreamWrapper(Stream stream, ILog log, EndPoint remoteEndPoint, bool useAuthentication, 
            Dictionary<byte, Type> messageTypeMap)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _messageTypeMap = messageTypeMap ?? _defaultMessageTypeMap;
            _remoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));

            AuthenticationEnabled = useAuthentication;

            if (useAuthentication)
            {
                _cts = new CancellationTokenSource();
                EnsureAuthenticatedAsync();
            }
        }

        /// <summary>
        /// Reads an incoming message.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{IMessageInfo}"/> which, when completed, 
        /// will contain information about the request and the message
        /// </returns>
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="StreamWrapper"/> is disposed</exception>
        /// <exception cref="InvalidDataException">Thrown when the message type or payload is invalid</exception>
        public async Task<IMessageInfo> ReadMessageAsync()
        {
            CheckDisposed();

            // 1 byte - message type, 4 bytes - request ID, 2 bytes - payload length
            var buffer = new byte[7];

            await _stream.FillBufferAsync(buffer);

            using (var memoryStream = new MemoryStream())
            using (var binaryReader = new BinaryReader(memoryStream))
            {
                await memoryStream.WriteAsync(buffer, 0, buffer.Length);

                memoryStream.Position = 0;

                var typeByte = binaryReader.ReadByte();

                if (!_messageTypeMap.ContainsKey(typeByte))
                    throw new InvalidDataException($"Message type {typeByte} has no handler");

                var result = new MessageInfo(this);

                result.Id = binaryReader.ReadUInt32();

                var payloadLength = binaryReader.ReadUInt16();
                buffer = new byte[payloadLength];

                await _stream.FillBufferAsync(buffer);

                try
                {
                    using (var tempMs = new MemoryStream(buffer))
                    {
                        var messageType = _messageTypeMap[typeByte];
                        result.Message = Serializer.NonGeneric.Deserialize(messageType, tempMs);

                        return result;
                    }
                }
                catch (Exception e)
                {
                    // Rethrow wrapped exception here to prevent having to catch specific library exceptions
                    throw new InvalidDataException("Error deserializing message payload. See inner exception for details",
                                                   e);
                }
            }
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        /// <typeparam name="T">The type of the message</typeparam>
        /// <param name="messageId">The ID of the request this message is a reply to</param>
        /// <param name="messageType">The message type</param>
        /// <param name="message">The message to send</param>
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="StreamWrapper"/> is disposed</exception>
        public async Task WriteMessageAsync<T>(uint messageId, byte messageType, T message)
        {
            CheckDisposed();

            byte[] payload;

            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, message);

                payload = ms.ToArray();
            }

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                bw.Write(messageType);
                bw.Write(messageId);
                bw.Write((ushort)payload.Length);
                bw.Write(payload);
                bw.Flush();

                ms.Position = 0;
                await ms.CopyToAsync(_stream);
            }
        }

        public void MarkAuthenticated()
        {
            IsAuthenticated = true;
        }

        /// <summary>
        /// Disposes the wrapper and closes the underlying connection
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        public override string ToString()
        {
            return $"{(ID ?? "UNKNOWN_ID")} ({_remoteEndPoint})";
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed) return;

            if(isDisposing)
            {
                _stream.Close();
                _stream.Dispose();

                _cts?.Cancel();
                _cts?.Dispose();
            }

            _isDisposed = true;
        }

        private async void EnsureAuthenticatedAsync()
        {
            try
            {
                await Task.Delay(10_000, _cts.Token);
            }
            catch(TaskCanceledException)
            {
                return;
            }

            if (_isDisposed) return;

            if (IsAuthenticated) return;

            await _log.WriteWarningAsync(nameof(StreamWrapper), nameof(EnsureAuthenticatedAsync),
                $"Client {ToString()} failed to authenticate within the time limit, disposing connection!");

            Dispose();
        }

        private void CheckDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(StreamWrapper), "The stream wrapper has been disposed");
        }
    }
}
