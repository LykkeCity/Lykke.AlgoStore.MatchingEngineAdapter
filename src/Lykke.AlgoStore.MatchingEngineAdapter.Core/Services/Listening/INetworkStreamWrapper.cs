using System;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening
{
    public interface INetworkStreamWrapper : IDisposable
    {
        IAsyncResult BeginReadMessage(AsyncCallback callback, object state);
        IMessageInfo EndReadMessage(IAsyncResult asyncResult);
        IMessageInfo ReadMessage();

        void WriteMessage<T>(uint messageId, byte messageType, T message);
    }
}
