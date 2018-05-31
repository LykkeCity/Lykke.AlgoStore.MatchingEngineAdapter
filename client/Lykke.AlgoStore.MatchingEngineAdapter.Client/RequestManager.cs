using Common;
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
        private readonly TasksManager<IMessageInfo> _taskManager = new TasksManager<IMessageInfo>();

        private string _clientId;
        private string _instanceId;

        private int _currentRequestId = -1;

        private bool _authorizationFailed;

        public RequestManager(IMeaCommunicator meaCommunicator)
        {
            _meaCommunicator = meaCommunicator ?? throw new ArgumentNullException(nameof(meaCommunicator));
            _meaCommunicator.OnMessageReceived += ProcessResponse;
            _meaCommunicator.OnConnectionEstablished += SendAuthRequest;
        }

        public void SetClientAndInstanceId(string clientId, string instanceId)
        {
            _clientId = clientId;
            _instanceId = instanceId;

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
            var request = new PingRequest { Message = $"{_clientId}_{_instanceId}" };

            var response = (await MakeRequestAsync(MeaRequestType.Ping, request)).Message as PingRequest;

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
