using Common.Log;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Requests;
using System;
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    internal class MatchingEngineAdapterClient : IMatchingEngineAdapterClient
    {
        private readonly IRequestManager _requestManager;
        private readonly ILog _log;

        public MatchingEngineAdapterClient(IRequestManager requestManager, ILog log)
        {
            _requestManager = requestManager ?? throw new ArgumentNullException(nameof(requestManager));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public Task<string> Ping(string content)
        {
            return Task.Run(() => PingSync(content));
        }

        private string PingSync(string content)
        {
            var pingRequest = new PingRequest { Message = content };

            _log.WriteInfo(nameof(MatchingEngineAdapterClient), nameof(PingSync), 
                           $"Sending MEA Ping request with content {content}");

            (var waitHandle, var requestId) = _requestManager.MakeRequest(MeaRequestType.Ping, pingRequest);

            waitHandle.WaitOne();

            var response = _requestManager.GetResponse(requestId) as PingRequest;

            return response?.Message;
        }
    }
}
