using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    public interface IMeaCommunicator
    {
        void SendRequest<T>(uint messageId, byte messageType, T message);
        event Action<IMessageInfo> OnMessageReceived;
    }
}
