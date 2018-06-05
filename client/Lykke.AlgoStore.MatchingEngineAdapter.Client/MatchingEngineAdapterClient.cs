using Common.Log;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using System;
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    internal class MatchingEngineAdapterClient : IMatchingEngineAdapterClient
    {
        private readonly IRequestManager _requestManager;
        private readonly ILog _log;

        public MatchingEngineAdapterClient(IRequestManager requestManager, ILog log)
        {
            _requestManager = requestManager ?? throw new ArgumentNullException(nameof(requestManager));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void SetClientAndInstanceId(string clientId, string instanceId)
        {
            _requestManager.SetClientAndInstanceId(clientId, instanceId);
        }

        public async Task<string> PingAsync(string content)
        {
            var pingRequest = new PingRequest { Message = content };

            await _log.WriteInfoAsync(nameof(MatchingEngineAdapterClient), nameof(PingAsync),
                           $"Sending MEA Ping request with content {content}");

            var message = await _requestManager.MakeRequestAsync(MeaRequestType.Ping, pingRequest);

            var response = message.Message as PingRequest;

            return response?.Message;
        }

        public async Task<ResponseModel<double>> PlaceMarketOrderAsync(string walletId, string assetPairId, OrderAction orderAction,
            double volume, bool straight, string instanceId, double? reservedLimitVolume = null)
        {
            var marketOrderRequest = new MarketOrderRequest
            {
                ClientId = walletId,
                AssetPairId = assetPairId,
                OrderAction = orderAction,
                Volume = volume,
                IsStraight = straight,
                InstanceId = instanceId
            };

            await _log.WriteInfoAsync(nameof(MatchingEngineAdapterClient), nameof(PlaceMarketOrderAsync),
                $"Sending MEA market order request for algo instance with Id {instanceId}");

            var message = await _requestManager.MakeRequestAsync(MeaRequestType.MarketOrderRequest, marketOrderRequest);

            var response = message.Message as ResponseModel<double>;

            return response;
        }
    }
}
