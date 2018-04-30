using System;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening
{
    public interface IProducerLoadBalancer : IDisposable
    {
        void AcceptConnection(IStreamWrapper connection);
    }
}
