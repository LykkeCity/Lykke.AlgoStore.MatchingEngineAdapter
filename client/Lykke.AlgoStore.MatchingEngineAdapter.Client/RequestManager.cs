using Common;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using System;
using System.Collections.Concurrent;
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

            _meaCommunicator.Start();
        }

        public async Task<Task<IMessageInfo>> MakeRequestAsync<T>(MeaRequestType requestType, T message)
        {
            var nextRequestId = GetNextRequestId();
            var task = _taskManager.Add(nextRequestId);

            await _meaCommunicator.SendRequestAsync(nextRequestId, (byte)requestType, message);

            return task;
        }

        private void ProcessResponse(IMessageInfo messageInfo)
        {
            if (messageInfo == null)
                throw new ArgumentNullException(nameof(messageInfo));

            _taskManager.SetResult(messageInfo.Id, messageInfo);
        }

        private void SendAuthRequest()
        {
            var request = new PingRequest { Message = $"{_clientId}_{_instanceId}" };

            var task = MakeRequestAsync(MeaRequestType.Ping, request);

            task.ContinueWith((t) =>
            {
                t.Result.ContinueWith((inner) =>
                {
                    var response = inner.Result.Message as PingRequest;

                    if (response.Message == "Fail")
                        throw new UnauthorizedAccessException("Authorization with MEA failed");
                });
            });
        }

        private uint GetNextRequestId()
        {
            return unchecked((uint)Interlocked.Increment(ref _currentRequestId));
        }
    }
}
