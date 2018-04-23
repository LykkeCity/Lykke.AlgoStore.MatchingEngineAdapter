using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Responses;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using System;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening
{
    /// <summary>
    /// Contains information about a given message
    /// </summary>
    internal class RequestInfo : IRequestInfo
    {
        private readonly IClientSocketWrapper _socket;

        /// <summary>
        /// The request ID
        /// </summary>
        public uint Id { get; set; }

        /// <summary>
        /// The message which came with the request
        /// </summary>
        public object Message { get; set; }

        /// <summary>
        /// Initializes a <see cref="RequestInfo"/> using a given <see cref="IClientSocketWrapper"/>
        /// </summary>
        /// <param name="socket">A <see cref="IClientSocketWrapper"/> to use for replying with messages</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="socket"/> is null</exception>
        public RequestInfo(IClientSocketWrapper socket)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        }

        /// <summary>
        /// Replies to this request with a given message
        /// </summary>
        /// <typeparam name="T">The object type of the <paramref name="message"/></typeparam>
        /// <param name="messageType">The type of the response</param>
        /// <param name="message">The message</param>
        public void Reply<T>(MeaResponseType messageType, T message)
        {
            _socket.WriteMessage(Id, messageType, message);
        }
    }
}
