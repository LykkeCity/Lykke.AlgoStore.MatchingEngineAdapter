using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Responses;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening
{
    public interface IMessageInfo
    {
        uint Id { get; set; }
        object Message { get; set; }

        void Reply<T>(MeaResponseType responseType, T message);
    }
}
