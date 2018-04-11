using System;
using Common.Log;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    public class MatchingEngineAdapterClient : IMatchingEngineAdapterClient, IDisposable
    {
        private readonly ILog _log;

        public MatchingEngineAdapterClient(string serviceUrl, ILog log)
        {
            _log = log;
        }

        public void Dispose()
        {
            //if (_service == null)
            //    return;
            //_service.Dispose();
            //_service = null;
        }
    }
}
