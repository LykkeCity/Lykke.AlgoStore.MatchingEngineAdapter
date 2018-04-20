using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Responses;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening
{
    public interface IClientSocketWrapper : IDisposable
    {
        IAsyncResult BeginReadMessage(AsyncCallback callback, object state);
        IRequestInfo EndReadMessage(IAsyncResult asyncResult);
        IRequestInfo ReadMessage();

        void WriteMessage<T>(uint requestId, MeaResponseType responseType, T message);
    }
}
