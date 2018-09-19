using System.Threading.Tasks;
using Lykke.MatchingEngine.Connector.Models.Api;
using OrderAction = Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.OrderAction;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services
{
    /// <summary>
    /// Adaptor service for the FeeCalculator used in MEA.
    /// </summary>
    public interface IFeeCalculatorAdapter
    {
        /// <summary>
        /// Calculate the fees for a limit order.
        /// </summary>
        /// <param name="clientId">the client id</param>
        /// <param name="assetPair">the asset-pair</param>
        /// <param name="orderAction">the action (Buy/Sell)</param>
        /// <returns></returns>
        Task<LimitOrderFeeModel[]> GetLimitOrderFees(string clientId, string assetPairId, OrderAction orderAction);

        /// <summary>
        /// Calculate the fees for a market order.
        /// </summary>
        /// <param name="clientId">the client id</param>
        /// <param name="assetPair">the asset-pair</param>
        /// <param name="orderAction">the action (Buy/Sell)</param>
        /// <returns></returns>
        Task<MarketOrderFeeModel[]> GetMarketOrderFees(string clientId, string assetPairId, OrderAction orderAction);
    }
}
