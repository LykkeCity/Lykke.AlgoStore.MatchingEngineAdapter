using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Responses;
using System;
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening
{
    /// <summary>
    /// Contains information about a given message
    /// </summary>
    internal class MessageInfo : IMessageInfo
    {
        private readonly IStreamWrapper _socket;

        /// <summary>
        /// The request ID
        /// </summary>
        public uint Id { get; set; }

        /// <summary>
        /// The message which came with the request
        /// </summary>
        public object Message { get; set; }

        /// <summary>
        /// The authentucation token which came with the request
        /// </summary>
        public string AuthToken { get; set; }

        /// <summary>
        /// Initializes a <see cref="MessageInfo"/> using a given <see cref="IStreamWrapper"/>
        /// </summary>
        /// <param name="socket">A <see cref="IStreamWrapper"/> to use for replying with messages</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="socket"/> is null</exception>
        public MessageInfo(IStreamWrapper socket)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            AuthToken = socket.ID;
        }

        /// <summary>
        /// Replies to this request with a given message
        /// </summary>
        /// <typeparam name="T">The object type of the <paramref name="message"/></typeparam>
        /// <param name="messageType">The type of the response</param>
        /// <param name="message">The message</param>
        public async Task ReplyAsync<T>(MeaResponseType messageType, T message)
        {
            await _socket.WriteMessageAsync(Id, (byte)messageType, message);
        }
    }
}
