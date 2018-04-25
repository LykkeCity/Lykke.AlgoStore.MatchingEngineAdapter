using System;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening
{
    public interface IProducerLoadBalancer : IDisposable
    {
        void AcceptConnection(INetworkStreamWrapper connection);
    }
}
