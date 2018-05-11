using System;
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening
{
    public interface IStreamWrapper : IDisposable
    {
        Task<IMessageInfo> ReadMessageAsync();

        void MarkAuthenticated();

        bool AuthenticationEnabled { get; }
        bool IsAuthenticated { get; }

        string ID { get; set; }

        Task WriteMessageAsync<T>(uint messageId, byte messageType, T message);
    }
}
