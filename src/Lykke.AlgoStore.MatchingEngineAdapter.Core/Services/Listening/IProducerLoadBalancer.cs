using System;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening
{
    public interface IProducerLoadBalancer : IDisposable
    {
        void AcceptConnection(INetworkStreamWrapper connection);
    }
}
