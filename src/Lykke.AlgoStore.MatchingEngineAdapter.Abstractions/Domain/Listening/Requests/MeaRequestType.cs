namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests
{
    public enum MeaRequestType
    {
        Ping = 0,
        MarketOrderRequest = 1,
        LimitOrderRequest = 2,
        CancelLimitOrderRequest = 3
    }
}
