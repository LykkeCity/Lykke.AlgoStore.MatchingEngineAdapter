using System;
using System.Threading.Tasks;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Contracts;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services
{
    public interface IMatchingEngineAdapter
    {
        Task<ResponseModel> CancelLimitOrderAsync(Guid limitOrderId, string instanceId);
        Task<ResponseModel<double>> HandleMarketOrderAsync(string clientId, string assetPairId, OrderAction orderAction, double volume, bool straight, string instanceId, double? reservedLimitVolume = default(double?));
        Task<ResponseModel<LimitOrderResponseModel>> PlaceLimitOrderAsync(string clientId, string assetPairId, OrderAction orderAction, double volume, double price, string instanceId, bool cancelPreviousOrders = false);
    }
}
