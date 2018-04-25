using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using System;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    /// <summary>
    /// Represents a MEA communicator which handles lower-level message writing/reading to the adapter
    /// </summary>
    public interface IMeaCommunicator
    {
        /// <summary>
        /// Starts the MEA communicator
        /// </summary>
        void Start();
        // Note: IStartable is not implemented on purpose because the method there is called automatically by AutoFac
        // and in this case we want to start it manually

        /// <summary>
        /// Sends a request to the MEA
        /// </summary>
        /// <typeparam name="T">The object type of the message</typeparam>
        /// <param name="messageId">The unique ID of the message</param>
        /// <param name="messageType">The type of them essage</param>
        /// <param name="message">The message to send</param>
        void SendRequest<T>(uint messageId, byte messageType, T message);

        /// <summary>
        /// Event which is fired when the connection to the MEA is opened
        /// </summary>
        event Action OnConnectionEstablished;

        /// <summary>
        /// Event which is fired when a message is received from the MEA
        /// </summary>
        event Action<IMessageInfo> OnMessageReceived;
    }
}
