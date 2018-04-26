using System.Threading.Tasks;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    /// <summary>
    /// The interface providing easy to use functions for communicating with the matching engine adapter
    /// </summary>
    public interface IMatchingEngineAdapterClient
    {
        /// <summary>
        /// Sets the current instance ID and client ID
        /// </summary>
        /// <param name="clientId">The client ID of the algo instance</param>
        /// <param name="instanceId">The ID of the algo instance</param>
        void SetClientAndInstanceId(string clientId, string instanceId);

        /// <summary>
        /// Sends a ping request to the matching engine adapter
        /// </summary>
        /// <param name="content">The message which the adapter will return</param>
        /// <returns>Task which will complete once the response is available</returns>
        Task<string> Ping(string content);

        /// <summary>
        /// Sends a market order request to the matching engine adapter
        /// </summary>
        /// <param name="walletId">The wallet Id</param>
        /// <param name="assetPairId">The asset pair Id</param>
        /// <param name="orderAction">The arder action (Buy/Sell)</param>
        /// <param name="volume">The volume to be traded</param>
        /// <param name="isStraight">Is order straight or reverse</param>
        /// <param name="instanceId">The algo instance Id</param>
        /// <param name="reservedLimitVolume">The reserved limit volume</param>
        /// <returns>A response model holding the market price</returns>
        Task<ResponseModel<double>> PlaceMarketOrder(string walletId, string assetPairId, OrderAction orderAction, double volume,
            bool isStraight, string instanceId, double? reservedLimitVolume = null);
    }
}
