using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening
{
    public interface IProducerLoadBalancer : IDisposable
    {
        void AcceptConnection(INetworkStreamWrapper connection);
    }
}
