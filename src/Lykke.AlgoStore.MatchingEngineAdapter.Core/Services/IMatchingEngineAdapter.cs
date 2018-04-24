using System.Threading.Tasks;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services
{
    public interface IMatchingEngineAdapter
    {
        //Task<ResponseModel> CancelLimitOrderAsync(Guid limitOrderId);
        Task<ResponseModel<double>> HandleMarketOrderAsync(string clientId, string assetPairId, OrderAction orderAction, double volume, bool straight, string instanceId, double? reservedLimitVolume = default(double?));
        //Task<ResponseModel<Guid>> PlaceLimitOrderAsync(string clientId, string assetPairId, OrderAction orderAction, double volume, double price, bool cancelPreviousOrders = false);
    }
}
