using Common.Log;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Repositories;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Contracts;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
using Lykke.MatchingEngine.Connector.Abstractions.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Enumerators;
using Lykke.Common.Log;
using Lykke.MatchingEngine.Connector.Models.Api;
using OrderAction = Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.OrderAction;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services
{
    public class MatchingEngineAdapter : IMatchingEngineAdapter
    {
        private readonly ILog _log;
        private readonly IMatchingEngineClient _matchingEngineClient;
        private readonly IAlgoInstanceTradeRepository _algoInstanceTradeRepository;
        private readonly IFeeCalculatorAdapter _feeCalculator;

        public MatchingEngineAdapter(IMatchingEngineClient matchingEngineClient,
            IAlgoInstanceTradeRepository algoClientInstanceRepository,
            IFeeCalculatorAdapter feeCalculator,
            ILogFactory logFactory)
        {
            _matchingEngineClient =
                matchingEngineClient ?? throw new ArgumentNullException(nameof(matchingEngineClient));
            _algoInstanceTradeRepository = algoClientInstanceRepository ??
                                           throw new ArgumentNullException(nameof(algoClientInstanceRepository));
            _feeCalculator = feeCalculator ?? throw new ArgumentNullException(nameof(feeCalculator));
            _log = logFactory.CreateLog(this);
        }

        public async Task<ResponseModel> CancelLimitOrderAsync(Guid limitOrderId)
        {
            var response = await _matchingEngineClient.CancelLimitOrderAsync(limitOrderId.ToString());
            await CheckResponseAndThrowIfNull(response);

            if (response.Status == MeStatusCodes.Ok)
            {
                //REMARK Update Order in Trading Service
                return ResponseModel.CreateOk();
            }

            return ConvertToApiModel(response.Status);
        }

        public async Task<ResponseModel<double>> HandleMarketOrderAsync(string clientId, string assetPairId,
            OrderAction orderAction, double volume,
            bool straight, string instanceId, double? reservedLimitVolume = null)
        {
            var order = new MarketOrderModel
            {
                Id = GetNextRequestId().ToString(),
                AssetPairId = assetPairId,
                ClientId = clientId,
                ReservedLimitVolume = reservedLimitVolume,
                Straight = straight,
                Volume = Math.Abs(volume),
                OrderAction = orderAction.ToMeOrderAction(),
                Fees = await _feeCalculator.GetMarketOrderFees(clientId, assetPairId, orderAction)
            };

            using (var cts = new CancellationTokenSource(10_000))
            {
                MarketOrderResponse response = null;

                try
                {
                    response = await _matchingEngineClient.HandleMarketOrderAsync(order, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Empty block, will throw on the check below
                }

                await CheckResponseAndThrowIfNull(response);

                if (response.Status == MeStatusCodes.Ok)
                {
                    await SaveTradeInDbAsync(order.Id, clientId, orderAction, volume, response.Price, instanceId,
                        OrderType.Market);

                    return ResponseModel<double>.CreateOk(response.Price);
                }

                return ConvertToApiModel<double>(response.Status);
            }
        }


        public async Task<ResponseModel<LimitOrderResponseModel>> PlaceLimitOrderAsync(string clientId,
            string assetPairId, OrderAction orderAction,
            double volume, double price, string instanceId, bool cancelPreviousOrders = false)
        {
            var requestId = GetNextRequestId();

            var order = new LimitOrderModel
            {
                Id = requestId.ToString(),
                AssetPairId = assetPairId,
                ClientId = clientId,
                Price = price,
                CancelPreviousOrders = cancelPreviousOrders,
                Volume = Math.Abs(volume),
                OrderAction = orderAction.ToMeOrderAction(),
                Fees = await _feeCalculator.GetLimitOrderFees(clientId, assetPairId, orderAction)
            };

            using (var cts = new CancellationTokenSource(10_000))
            {
                MeResponseModel response = null;

                try
                {
                    response = await _matchingEngineClient.PlaceLimitOrderAsync(order, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Empty block, will throw on the check below
                }

                await CheckResponseAndThrowIfNull(response);
                var result = new LimitOrderResponseModel
                {
                    Id = requestId
                };

                if (response.Status == MeStatusCodes.Ok)
                {
                    //REMARK: Price is set to NULL for limit order cause it does not come as response from ME
                    await SaveTradeInDbAsync(order.Id, clientId, orderAction, volume, null, instanceId,
                        OrderType.Limit);

                    return ResponseModel<LimitOrderResponseModel>.CreateOk(result);
                }

                var responseModel = ConvertToApiModel<LimitOrderResponseModel>(response.Status);
                responseModel.Result = result;
                return responseModel;
            }
        }

        private static Guid GetNextRequestId() => Guid.NewGuid();

        private async Task CheckResponseAndThrowIfNull(object response)
        {
            if (response == null)
            {
                var exception = new InvalidOperationException("ME not available");

                _log.Error(nameof(MatchingEngineAdapter), exception, nameof(CancelLimitOrderAsync));

                throw exception;
            }
        }

        private ResponseModel ConvertToApiModel(MeStatusCodes status)
        {
            if (status == MeStatusCodes.Ok)
                return ResponseModel.CreateOk();

            return ResponseModel.CreateFail(GetErrorCodeType(status));
        }

        private ResponseModel<T> ConvertToApiModel<T>(MeStatusCodes status)
        {
            return ResponseModel<T>.CreateFail(GetErrorCodeType(status));
        }

        private async Task SaveTradeInDbAsync(string orderId, string walletId, OrderAction orderAction, double volume,
            double? price, string instanceId, OrderType orderType)
        {
            await _algoInstanceTradeRepository.CreateAlgoInstanceOrderAsync(
                new CSharp.AlgoTemplate.Models.Models.AlgoInstanceTrade
                {
                    OrderId = orderId,
                    WalletId = walletId,
                    IsBuy = orderAction == OrderAction.Buy,
                    Amount = volume,
                    Price = price,
                    InstanceId = instanceId,
                    OrderType = orderType
                });
        }

        private ErrorCodeType GetErrorCodeType(MeStatusCodes code)
        {
            switch (code)
            {
                case MeStatusCodes.Ok:
                    throw new InvalidOperationException("Ok is not an error.");
                case MeStatusCodes.LowBalance:
                    return ErrorCodeType.LowBalance;
                case MeStatusCodes.AlreadyProcessed:
                    return ErrorCodeType.AlreadyProcessed;
                case MeStatusCodes.UnknownAsset:
                    return ErrorCodeType.UnknownAsset;
                case MeStatusCodes.NoLiquidity:
                    return ErrorCodeType.NoLiquidity;
                case MeStatusCodes.NotEnoughFunds:
                    return ErrorCodeType.NotEnoughFunds;
                case MeStatusCodes.Dust:
                    return ErrorCodeType.Dust;
                case MeStatusCodes.ReservedVolumeHigherThanBalance:
                    return ErrorCodeType.ReservedVolumeHigherThanBalance;
                case MeStatusCodes.NotFound:
                    return ErrorCodeType.NotFound;
                case MeStatusCodes.BalanceLowerThanReserved:
                    return ErrorCodeType.BalanceLowerThanReserved;
                case MeStatusCodes.LeadToNegativeSpread:
                    return ErrorCodeType.LeadToNegativeSpread;
                case MeStatusCodes.TooSmallVolume:
                    return ErrorCodeType.Dust;
                case MeStatusCodes.InvalidFee:
                    return ErrorCodeType.InvalidFee;
                case MeStatusCodes.Duplicate:
                    return ErrorCodeType.Duplicate;
                case MeStatusCodes.Runtime:
                    return ErrorCodeType.Runtime;
                case MeStatusCodes.BadRequest:
                    return ErrorCodeType.BadRequest;
                case MeStatusCodes.InvalidPrice:
                    return ErrorCodeType.InvalidPrice;
                case MeStatusCodes.Replaced:
                    return ErrorCodeType.Replaced;
                case MeStatusCodes.NotFoundPrevious:
                    return ErrorCodeType.NotFoundPrevious;
                default:
                    _log.Warning(nameof(GetErrorCodeType), $"Unknown ME status code {code}",
                        context: nameof(MatchingEngineAdapter));
                    return ErrorCodeType.Runtime;
            }
        }
    }
}
