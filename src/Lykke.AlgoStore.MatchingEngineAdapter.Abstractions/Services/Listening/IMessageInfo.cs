
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Responses;
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening
{
    public interface IMessageInfo
    {
        uint Id { get; set; }
        object Message { get; set; }

        string AuthToken { get; set; }

        Task ReplyAsync<T>(MeaResponseType responseType, T message);
    }
}
