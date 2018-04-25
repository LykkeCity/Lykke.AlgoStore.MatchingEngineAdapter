﻿using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
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

        private void SendAuthRequest()
        {
            var request = new PingRequest { Message = $"{_clientId}_{_instanceId}" };

            (var waitHandle, var requestId) = MakeRequest(MeaRequestType.Ping, request);

            ThreadPool.QueueUserWorkItem((state) => 
            {
                waitHandle.WaitOne();

                var response = GetResponse(requestId) as PingRequest;

                // This will cause an unhandled exception and exit/crash the algo instance
                // It is unlikely that an honest instance will get into a situation where it can't authenticate
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
