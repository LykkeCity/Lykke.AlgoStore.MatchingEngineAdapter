using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening
{
    public interface IMessageHandler
    {
        Task HandleMessage(IMessageInfo messageInfo);
    }
}
