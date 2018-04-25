using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening
{
    public interface IMessageQueue
    {
        void Enqueue(IMessageInfo request);
        IMessageInfo Dequeue(CancellationToken cancellationToken);
    }
}
