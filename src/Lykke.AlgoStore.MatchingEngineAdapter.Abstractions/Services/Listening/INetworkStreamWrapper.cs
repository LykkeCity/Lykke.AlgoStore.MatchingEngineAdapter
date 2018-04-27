using System;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening
{
    public interface INetworkStreamWrapper : IDisposable
    {
        IAsyncResult BeginReadMessage(AsyncCallback callback, object state);
        IMessageInfo EndReadMessage(IAsyncResult asyncResult);
        IMessageInfo ReadMessage();

        void MarkAuthenticated();

        bool AuthenticationEnabled { get; }
        bool IsAuthenticated { get; }

        string ID { get; set; }

        void WriteMessage<T>(uint messageId, byte messageType, T message);
    }
}
