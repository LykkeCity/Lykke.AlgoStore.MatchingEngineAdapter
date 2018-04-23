using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Requests;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    public interface IRequestManager
    {
        (WaitHandle waitHandle, uint requestId) MakeRequest<T>(MeaRequestType requestType, T message);
        object GetResponse(uint requestId);
    }
}
