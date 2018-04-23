using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Requests;
using System;
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    internal class MatchingEngineAdapterClient : IMatchingEngineAdapterClient
    {
        private readonly IRequestManager _requestManager;

        public MatchingEngineAdapterClient(IRequestManager requestManager)
        {
            _requestManager = requestManager ?? throw new ArgumentNullException(nameof(requestManager));
        }

        public Task<string> Ping(string content)
        {
            return Task.Run(() => PingSync(content));
        }

        private string PingSync(string content)
        {
            var pingRequest = new PingRequest { Message = content };

            (var waitHandle, var requestId) = _requestManager.MakeRequest(MeaRequestType.Ping, pingRequest);

            waitHandle.WaitOne();

            var response = _requestManager.GetResponse(requestId) as PingRequest;

            return response?.Message;
        }
    }
}
