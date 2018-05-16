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
        private readonly ConcurrentDictionary<uint, TaskCompletionSource<IMessageInfo>> 
            _queuedMessages = new ConcurrentDictionary<uint, TaskCompletionSource<IMessageInfo>>();

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

        public Task<IMessageInfo> MakeRequestAsync<T>(MeaRequestType requestType, T message)
        {
            var taskCompletionSource = new TaskCompletionSource<IMessageInfo>();
            var nextRequestId = GetNextRequestId();

            _queuedMessages.TryAdd(nextRequestId, taskCompletionSource);
            // Wait here because otherwise we can't return the task from the completion source
            _meaCommunicator.SendRequestAsync(nextRequestId, (byte)requestType, message).Wait();

            return taskCompletionSource.Task;
        }

        private void ProcessResponse(IMessageInfo messageInfo)
        {
            if (messageInfo == null)
                throw new ArgumentNullException(nameof(messageInfo));

            if (!_queuedMessages.ContainsKey(messageInfo.Id))
                return;

            TaskCompletionSource<IMessageInfo> taskCompletionSource;

            if (!_queuedMessages.TryRemove(messageInfo.Id, out taskCompletionSource))
                return;

            taskCompletionSource.SetResult(messageInfo);
        }

        private void SendAuthRequest()
        {
            var request = new PingRequest { Message = $"{_clientId}_{_instanceId}" };

            var task = MakeRequestAsync(MeaRequestType.Ping, request);

            task.ContinueWith((t) =>
            {
                var response = t.Result.Message as PingRequest;

                if (response.Message == "Fail")
                    throw new UnauthorizedAccessException("Authorization with MEA failed");
            });
        }

        private uint GetNextRequestId()
        {
            return unchecked((uint)Interlocked.Increment(ref _currentRequestId));
        }
    }
}
