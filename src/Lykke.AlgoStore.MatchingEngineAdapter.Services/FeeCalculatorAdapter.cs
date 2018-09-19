using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
using Lykke.Service.Assets.Client;
using Lykke.Service.FeeCalculator.Client;
using System;
using System.Threading.Tasks;
using Lykke.MatchingEngine.Connector.Models.Api;
using Lykke.MatchingEngine.Connector.Models.Common;
using FeeType = Lykke.Service.FeeCalculator.AutorestClient.Models.FeeType;
using OrderAction = Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.OrderAction;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services
{
    public class FeeCalculatorAdapter : IFeeCalculatorAdapter
    {
        private readonly IFeeCalculatorClient _feeCalculatorClient;
        private readonly IAssetsService _assetsService;
        private readonly string _feeSettingsTargetClientIdHft;

        public FeeCalculatorAdapter(IFeeCalculatorClient feeCalculatorClient, IAssetsService assetsService,
            string feeSettingsTargetClientIdHft)
        {
            _feeCalculatorClient = feeCalculatorClient ?? throw new ArgumentNullException(nameof(feeCalculatorClient));
            _assetsService = assetsService ?? throw new ArgumentNullException(nameof(assetsService));
            _feeSettingsTargetClientIdHft = feeSettingsTargetClientIdHft ??
                                            throw new ArgumentNullException(nameof(_feeSettingsTargetClientIdHft));
        }

        public async Task<MarketOrderFeeModel[]> GetMarketOrderFees(string clientId, string assetPairId,
            OrderAction orderAction)
        {
            var assetPair = await _assetsService.AssetPairGetAsync(assetPairId);
            var fee = await _feeCalculatorClient.GetMarketOrderAssetFee(clientId, assetPair.Id, assetPair.BaseAssetId,
                orderAction.ToFeeOrderAction());

            var model = new MarketOrderFeeModel
            {
                Size = (double) fee.Amount,
                SizeType = GetFeeSizeType(fee.Type),
                SourceClientId = clientId,
                TargetClientId = fee.TargetWalletId ?? _feeSettingsTargetClientIdHft,
                Type = fee.Amount == 0m
                    ? MatchingEngine.Connector.Models.Common.FeeType.NO_FEE
                    : MatchingEngine.Connector.Models.Common.FeeType.CLIENT_FEE,
                AssetId = string.IsNullOrEmpty(fee.TargetAssetId)
                    ? Array.Empty<string>()
                    : new[] {fee.TargetAssetId}
            };

            return new[] {model};
        }

        public async Task<LimitOrderFeeModel[]> GetLimitOrderFees(string clientId, string assetPairId,
            OrderAction orderAction)
        {
            var assetPair = await _assetsService.AssetPairGetAsync(assetPairId);
            var fee = await _feeCalculatorClient.GetLimitOrderFees(clientId, assetPairId, assetPair?.BaseAssetId,
                orderAction.ToFeeOrderAction());

            var model = new LimitOrderFeeModel
            {
                MakerSize = (double) fee.MakerFeeSize,
                TakerSize = (double) fee.TakerFeeSize,
                SourceClientId = clientId,
                TargetClientId = _feeSettingsTargetClientIdHft,
                Type = fee.MakerFeeSize == 0m && fee.TakerFeeSize == 0m
                    ? MatchingEngine.Connector.Models.Common.FeeType.NO_FEE
                    : MatchingEngine.Connector.Models.Common.FeeType.CLIENT_FEE,
                MakerFeeModificator = (double) fee.MakerFeeModificator,
                MakerSizeType = GetFeeSizeType(fee.MakerFeeType),
                TakerSizeType = GetFeeSizeType(fee.TakerFeeType),
                AssetId = Array.Empty<string>()
            };

            return new[] {model};
        }

        private static FeeSizeType GetFeeSizeType(FeeType type)
            => type == FeeType.Absolute
                ? FeeSizeType.ABSOLUTE
                : FeeSizeType.PERCENTAGE;
    }
}
