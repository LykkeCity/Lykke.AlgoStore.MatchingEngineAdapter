using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    /// <summary>
    /// Represents a request manager which uses WaitHandles to signal when a response has been received
    /// </summary>
    public interface IRequestManager
    {
        /// <summary>
        /// Sets the current instance ID and client ID
        /// </summary>
        /// <param name="clientId">The client ID of the algo instance</param>
        /// <param name="instanceId">The ID of the algo instance</param>
        void SetClientAndInstanceId(string clientId, string instanceId);

        /// <summary>
        /// Makes a request to the MEA
        /// </summary>
        /// <typeparam name="T">The object type of the message</typeparam>
        /// <param name="requestType">The type of the request</param>
        /// <param name="message">The message to send</param>
        /// <returns>
        /// A <see cref="WaitHandle"/> which can be used to wait for the response and
        /// the unique request ID which must be used to retrieve the response later
        /// </returns>
        (WaitHandle, uint) MakeRequest<T>(MeaRequestType requestType, T message);

        /// <summary>
        /// Retrieves the response for a given request
        /// </summary>
        /// <param name="requestId">The request ID to retrieve the response for</param>
        /// <returns>The response</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// Thrown when the <paramref name="requestId"/> is invalid or the response has not been received yet
        /// </exception>
        object GetResponse(uint requestId);
    }
}
