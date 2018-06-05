using Common;
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services
{
    public interface IListeningService : IStopable
    {
        Task Start();
    }
}
