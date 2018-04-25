
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Responses;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening
{
    public interface IMessageInfo
    {
        uint Id { get; set; }
        object Message { get; set; }

        void Reply<T>(MeaResponseType responseType, T message);
    }
}
