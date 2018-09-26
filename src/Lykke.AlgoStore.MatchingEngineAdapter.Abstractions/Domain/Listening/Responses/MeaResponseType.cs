namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Responses
{
    public enum MeaResponseType
    {
        Pong = 0,
        MarketOrderResponse = 1,
        LimitOrderResponse = 2,
        CancelLimitOrderResponse = 3
    }
}
