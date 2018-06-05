using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services
{
    public interface IShutdownManager
    {
        Task StopAsync();
    }
}
