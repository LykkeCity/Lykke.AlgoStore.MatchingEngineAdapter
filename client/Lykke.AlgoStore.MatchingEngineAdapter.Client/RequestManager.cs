using Common;
using Common.Log;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    internal class RequestManager : IRequestManager
    {
        private readonly IMeaCommunicator _meaCommunicator;
        private readonly ILog _log;
        private readonly TasksManager<IMessageInfo> _taskManager = new TasksManager<IMessageInfo>();

        private string _authToken;

        private int _currentRequestId = -1;

        private bool _authorizationFailed;

        public RequestManager(IMeaCommunicator meaCommunicator, ILog log)
        {
            _meaCommunicator = meaCommunicator ?? throw new ArgumentNullException(nameof(meaCommunicator));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _meaCommunicator.OnMessageReceived += ProcessResponse;
            _meaCommunicator.OnConnectionEstablished += SendAuthRequest;
        }

        public void SetAuthToken(string authToken)
        {
            _authToken = authToken;

            _authorizationFailed = false;

            _meaCommunicator.Start();
        }

        public async Task<IMessageInfo> MakeRequestAsync<T>(MeaRequestType requestType, T message)
        {
            if (_authorizationFailed)
                throw new UnauthorizedAccessException("Authorization with MEA failed");

            var nextRequestId = GetNextRequestId();
            var task = _taskManager.Add(nextRequestId);

            await _meaCommunicator.SendRequestAsync(nextRequestId, (byte)requestType, message);

            return await task;
        }

        private void ProcessResponse(IMessageInfo messageInfo)
        {
            _taskManager.SetResult(messageInfo.Id, messageInfo);
        }

        private async void SendAuthRequest()
        {
            var request = new PingRequest { Message = $"{_authToken}" };

            PingRequest response;

            try
            {
                var messageInfo = await MakeRequestAsync(MeaRequestType.Ping, request);
                response = messageInfo.Message as PingRequest;

                // Unlikely to happen, but if for some reason the MEA returns a different message,
                // return from this method
                if (response == null)
                    return;
            }
            catch(Exception e)
            {
                // Cases when MakeRequestAsync can throw should be only network failures,
                // in which case the connection will be retried anyway, but catch and log
                // all exceptions in case something unexpected comes up
                await _log.WriteErrorAsync(nameof(RequestManager), nameof(SendAuthRequest), e);
                return;
            }

            if (response.Message == "Fail")
            {
                _authorizationFailed = true;
                await _meaCommunicator.Stop();
                _taskManager.CancelAll();
            }
        }

        private uint GetNextRequestId()
        {
            return unchecked((uint)Interlocked.Increment(ref _currentRequestId));
        }
    }
}
