using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Responses;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.AlgoStore.Job.Stopping.Client;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain;
using MarketOrderRequest = Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests.MarketOrderRequest;
using Common.Log;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening
{
    /// <summary>
    /// Processes queued messages and sends appropriate replies
    /// </summary>
    public class MessageHandler : IMessageHandler
    {
        private readonly Dictionary<Type, Func<IMessageInfo, Task>> _messageHandlers = new Dictionary<Type, Func<IMessageInfo, Task>>();
        private readonly IMatchingEngineAdapter _matchingEngineAdapter;
        private readonly IAlgoInstanceStoppingClient _algoInstanceStoppingClient;
        private readonly ILog _log;

        /// <summary>
        /// Initializes a <see cref="MessageHandler"/>
        /// </summary>
        /// <param name="matchingEngineAdapter">A <see cref="IMatchingEngineAdapter"/> to use for communication with the ME</param>
        /// <param name="algoInstanceStoppingClient">A<see cref="IAlgoInstanceStoppingClient"/> to call AlgoStore stopping service</param>
        /// <param name="log">An <see cref="ILog"/> to use for logging</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="matchingEngineAdapter"/> is null
        /// </exception>
        public MessageHandler(
            IMatchingEngineAdapter matchingEngineAdapter, 
            IAlgoInstanceStoppingClient algoInstanceStoppingClient,
            ILog log)
        {
            _matchingEngineAdapter =
                matchingEngineAdapter ?? throw new ArgumentNullException(nameof(matchingEngineAdapter));

            _messageHandlers.Add(typeof(PingRequest), PingHandler);
            _messageHandlers.Add(typeof(MarketOrderRequest), MarketOrderRequestHandler);
            _messageHandlers.Add(typeof(LimitOrderRequest), LimitOrderRequestHandler);
            _messageHandlers.Add(typeof(CancelLimitOrderRequest), CancelLimitOrderRequestHandler);
            _algoInstanceStoppingClient = algoInstanceStoppingClient;
            _log = log;
        }

        public async Task HandleMessage(IMessageInfo messageInfo)
        {
            var messageType = messageInfo.Message.GetType();

            if (_messageHandlers.ContainsKey(messageType))
                await _messageHandlers[messageType](messageInfo);
        }

        /// <summary>
        /// Handles a <see cref="PingRequest"/> by replying with <see cref="MeaResponseType.Pong"/> containing
        /// the same message
        /// </summary>
        /// <param name="request">The <see cref="IMessageInfo"/> containing the message</param>
        private async Task PingHandler(IMessageInfo request)
        {
            var msg = (PingRequest)request.Message;

            await _log.WriteInfoAsync(nameof(MessageHandler), nameof(PingHandler),
                $"Received ping message from {request.AuthToken} containing {msg.Message}");

            await request.ReplyAsync(MeaResponseType.Pong, msg);
        }

        /// <summary>
        /// Handles a <see cref="MarketOrderRequest"/> by replying with <see cref="ResponseModel{T}"/> containing
        /// the response message />
        /// </summary>
        /// <param name="request">The <see cref="MIessageInfo"/> containing the message</param>
        private async Task MarketOrderRequestHandler(IMessageInfo request)
        {
            var msg = (MarketOrderRequest)request.Message;

            await _log.WriteInfoAsync(nameof(MessageHandler), nameof(MarketOrderRequestHandler),
                $"Received market order from {request.AuthToken}: {msg.OrderAction.ToString()} {msg.Volume} {msg.AssetPairId}, " +
                $"Straight: {msg.IsStraight}, " +
                $"Instance ID: {msg.InstanceId}");

            var result = await _matchingEngineAdapter.HandleMarketOrderAsync(msg.ClientId, msg.AssetPairId, msg.OrderAction,
                msg.Volume, msg.IsStraight, msg.InstanceId);

            await _log.WriteInfoAsync(nameof(MessageHandler), nameof(MarketOrderRequestHandler),
                $"Received response for market order of instance {msg.InstanceId}, token {request.AuthToken}: " +
                $"Valid: {result.Error == null}, " +
                $"Error: {(result.Error == null ? null : $"Error Code: {result.Error.Code.ToString()}, Field: {result.Error.Field}, Message: {result.Error.Message}")}");

            await request.ReplyAsync(MeaResponseType.MarketOrderResponse, result);

            if (result.Error != null && result.Error.Code == ErrorCodeType.NotEnoughFunds)
            {
                await _log.WriteInfoAsync(nameof(MessageHandler), nameof(MarketOrderRequestHandler),
                    $"Instance {msg.InstanceId}, token {request.AuthToken} is out of funds, stopping...");
                await _algoInstanceStoppingClient.DeleteAlgoInstanceAsync(msg.InstanceId, request.AuthToken);
            }
        }

        /// <summary>
        /// Handles a <see cref="LimitOrderRequest"/> by replying with <see cref="ResponseModel{T}"/> containing
        /// the response message />
        /// </summary>
        /// <param name="request">The <see cref="MIessageInfo"/> containing the message</param>
        private async Task LimitOrderRequestHandler(IMessageInfo request)
        {
            var msg = (LimitOrderRequest)request.Message;

            await _log.WriteInfoAsync(nameof(MessageHandler), nameof(LimitOrderRequestHandler),
               $"Received limit order from {request.AuthToken}: {msg.OrderAction.ToString()} {msg.Volume} {msg.AssetPairId}, " +
               $"Price: {msg.Price}, " +
               $"Instance ID: {msg.InstanceId}");

            var result = await _matchingEngineAdapter.PlaceLimitOrderAsync(msg.ClientId, msg.AssetPairId, msg.OrderAction,
                msg.Volume, msg.Price, msg.InstanceId, msg.CancelPreviousOrders);

            await _log.WriteInfoAsync(nameof(MessageHandler), nameof(LimitOrderRequestHandler),
               $"Received response for limit order of instance {msg.InstanceId}, token {request.AuthToken}: " +
               $"Valid: {result.Error == null}, " +
               $"Error: {(result.Error == null ? null : $"Error Code: {result.Error.Code.ToString()}, Field: {result.Error.Field}, Message: {result.Error.Message}")}");

            await request.ReplyAsync(MeaResponseType.LimitOrderResponse, result);

            if (result.Error != null && result.Error.Code == ErrorCodeType.NotEnoughFunds)
            {
                await _log.WriteInfoAsync(nameof(MessageHandler), nameof(LimitOrderRequestHandler),
                    $"Instance {msg.InstanceId}, token {request.AuthToken} is out of funds, stopping...");
                await _algoInstanceStoppingClient.DeleteAlgoInstanceAsync(msg.InstanceId, request.AuthToken);
            }
        }

        private async Task CancelLimitOrderRequestHandler(IMessageInfo request)
        {
            var msg = (CancelLimitOrderRequest)request.Message;

            await _log.WriteInfoAsync(nameof(MessageHandler), nameof(CancelLimitOrderRequestHandler),
               $"Received limit order cancellation from {request.AuthToken}, " +
               $"Limit Order Id: {msg.LimitOrderId}, " +
               $"Instance ID: {msg.InstanceId}");

            var result = await _matchingEngineAdapter.CancelLimitOrderAsync(msg.LimitOrderId, msg.InstanceId);

            await _log.WriteInfoAsync(nameof(MessageHandler), nameof(CancelLimitOrderRequestHandler),
               $"Received response for limit order cancellation of instance {msg.InstanceId}, token {request.AuthToken}: " +
               $"Valid: {result.Error == null}, " +
               $"Error: {(result.Error == null ? null : $"Error Code: {result.Error.Code.ToString()}, Field: {result.Error.Field}, Message: {result.Error.Message}")}");

            await request.ReplyAsync(MeaResponseType.CancelLimitOrderResponse, result);
        }
    }
}
