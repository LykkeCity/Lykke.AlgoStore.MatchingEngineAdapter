using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using System.Threading.Tasks;

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
        /// A task which when completed will contain the response
        /// </returns>
        Task<Task<IMessageInfo>> MakeRequestAsync<T>(MeaRequestType requestType, T message);
    }
}
