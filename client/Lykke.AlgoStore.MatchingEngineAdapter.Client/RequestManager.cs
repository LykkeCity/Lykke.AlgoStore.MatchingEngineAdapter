using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    internal class RequestManager : IRequestManager
    {
        private readonly IMeaCommunicator _meaCommunicator;

        private readonly ConcurrentDictionary<uint, object> _responses = new ConcurrentDictionary<uint, object>();
        private readonly ConcurrentDictionary<uint, ManualResetEvent> _waitHandles = new ConcurrentDictionary<uint, ManualResetEvent>();

        private int _currentRequestId = -1;

        public RequestManager(IMeaCommunicator meaCommunicator)
        {
            _meaCommunicator = meaCommunicator ?? throw new ArgumentNullException(nameof(meaCommunicator));
            _meaCommunicator.OnMessageReceived += ProcessResponse;
        }

        public (WaitHandle, uint) MakeRequest<T>(MeaRequestType requestType, T message)
        {
            var manualResetEvent = new ManualResetEvent(false);
            var nextRequestId = GetNextRequestId();

            _waitHandles.TryAdd(nextRequestId, manualResetEvent);
            _meaCommunicator.SendRequest(nextRequestId, (byte)requestType, message);

            return (manualResetEvent, nextRequestId);
        }

        public object GetResponse(uint requestId)
        {
            object response;

            if (!_responses.TryRemove(requestId, out response))
                throw new KeyNotFoundException($"A message with the ID {requestId} does not exist");

            return response;
        }

        private void ProcessResponse(IMessageInfo messageInfo)
        {
            if (messageInfo == null)
                throw new ArgumentNullException(nameof(messageInfo));

            if (!_waitHandles.ContainsKey(messageInfo.Id))
                return;

            ManualResetEvent manualResetEvent;

            if (!_waitHandles.TryRemove(messageInfo.Id, out manualResetEvent))
                return;

            _responses.TryAdd(messageInfo.Id, messageInfo.Message);
            manualResetEvent.Set();
        }

        private uint GetNextRequestId()
        {
            return unchecked((uint)Interlocked.Increment(ref _currentRequestId));
        }
    }
}
