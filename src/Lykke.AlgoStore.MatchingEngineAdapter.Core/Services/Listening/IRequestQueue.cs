using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening
{
    public interface IRequestQueue
    {
        void Enqueue(IRequestInfo request);
        IRequestInfo Dequeue(CancellationToken cancellationToken);
    }
}
