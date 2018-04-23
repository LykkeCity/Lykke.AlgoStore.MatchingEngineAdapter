using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    public interface IMatchingEngineAdapterClient
    {
        Task<string> Ping(string content);
    }
}
