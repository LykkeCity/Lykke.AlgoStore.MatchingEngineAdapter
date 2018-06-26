using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Responses;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Repositories;
using Lykke.AlgoStore.Job.Stopping.Client;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain;
using MarketOrderRequest = Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests.MarketOrderRequest;

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

        /// <summary>
        /// Initializes a <see cref="MessageHandler"/>
        /// </summary>
        /// <param name="matchingEngineAdapter">A <see cref="IMatchingEngineAdapter"/> to use for communication with the ME</param>
        /// <param name="algoInstanceStoppingClient">A<see cref="IAlgoInstanceStoppingClient"/> to call AlgoStore stopping service</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="matchingEngineAdapter"/> is null
        /// </exception>
        public MessageHandler(IMatchingEngineAdapter matchingEngineAdapter, IAlgoInstanceStoppingClient algoInstanceStoppingClient)
        {
            _matchingEngineAdapter =
                matchingEngineAdapter ?? throw new ArgumentNullException(nameof(matchingEngineAdapter));

            _messageHandlers.Add(typeof(PingRequest), PingHandler);
            _messageHandlers.Add(typeof(MarketOrderRequest), MarketOrderRequestHandler);
            _algoInstanceStoppingClient = algoInstanceStoppingClient;
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

            var result = await _matchingEngineAdapter.HandleMarketOrderAsync(msg.ClientId, msg.AssetPairId, msg.OrderAction,
                msg.Volume, msg.IsStraight, msg.InstanceId);

            await request.ReplyAsync(MeaResponseType.MarketOrderResponse, result);

            if (result.Error != null && result.Error.Code == ResponseModel.ErrorCodeType.NotEnoughFunds)
            {
                var deleteResult = await _algoInstanceStoppingClient.DeleteAlgoInstanceAsync(msg.InstanceId, request.AuthToken);

                if (!deleteResult.IsSuccessfulDeletion || !string.IsNullOrEmpty(deleteResult.ErrorMessage))
                {

                }
            }
        }
    }
}
