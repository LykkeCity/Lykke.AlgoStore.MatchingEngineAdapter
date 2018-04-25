using System.Threading;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening
{
    public interface IMessageQueue
    {
        void Enqueue(IMessageInfo request);
        IMessageInfo Dequeue(CancellationToken cancellationToken);
    }
}
