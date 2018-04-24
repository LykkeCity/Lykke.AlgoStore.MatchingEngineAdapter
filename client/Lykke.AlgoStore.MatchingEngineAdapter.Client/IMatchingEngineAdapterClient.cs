using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    /// <summary>
    /// The interface providing easy to use functions for communicating with the matching engine adapter
    /// </summary>
    public interface IMatchingEngineAdapterClient
    {
        /// <summary>
        /// Sends a ping request to the matching engine adapter
        /// </summary>
        /// <param name="content">The message which the adapter will return</param>
        /// <returns>Task which will complete once the response is available</returns>
        Task<string> Ping(string content);
    }
}
