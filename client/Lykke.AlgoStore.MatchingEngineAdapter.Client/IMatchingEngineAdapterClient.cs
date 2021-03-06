﻿using System;
using System.Threading.Tasks;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Contracts;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    /// <summary>
    /// The interface providing easy to use functions for communicating with the matching engine adapter
    /// </summary>
    public interface IMatchingEngineAdapterClient
    {
        /// <summary>
        /// Sets the current instance authentication token
        /// </summary>
        /// <param name="authToken">The auth token of the algo instance</param>
        void SetAuthToken(string authToken);

        /// <summary>
        /// Sends a ping request to the matching engine adapter
        /// </summary>
        /// <param name="content">The message which the adapter will return</param>
        /// <returns>Task which will complete once the response is available</returns>
        Task<string> PingAsync(string content);

        /// <summary>
        /// Sends a market order request to the matching engine adapter
        /// </summary>
        /// <param name="walletId">The wallet Id</param>
        /// <param name="assetPairId">The asset pair Id</param>
        /// <param name="orderAction">The arder action (Buy/Sell)</param>
        /// <param name="volume">The volume to be traded</param>
        /// <param name="isStraight">Is order straight or reverse</param>
        /// <param name="instanceId">The algo instance Id</param>
        /// <param name="reservedLimitVolume">The reserved limit volume</param>
        /// <returns>A response model holding the market price</returns>
        Task<ResponseModel<double>> PlaceMarketOrderAsync(string walletId, string assetPairId, OrderAction orderAction,
            double volume, bool isStraight, string instanceId, double? reservedLimitVolume = null);


        /// <summary>
        /// Sends a limit order request to the matching engine adapter
        /// </summary>
        /// <param name="walletId">The wallet Id</param>
        /// <param name="assetPairId">The asset pair Id</param>
        /// <param name="orderAction">The arder action (Buy/Sell)</param>
        /// <param name="volume">The volume to be traded</param>
        /// <param name="price">The limit price</param>
        /// <param name="instanceId">The algo instance Id</param>
        /// <param name="cancelPreviousOrders">Cancel previous orders? (optional)</param>
        /// <returns>A response model holding the limit order Id</returns>
        Task<ResponseModel<LimitOrderResponseModel>> PlaceLimitOrderAsync(string walletId, string assetPairId, OrderAction orderAction,
            double volume, double price, string instanceId, bool cancelPreviousOrders = false);

        /// <summary>
        /// Cancel limit order
        /// </summary>
        /// <param name="limitOrderId">The Id of the limit order</param>
        /// <param name="instanceId">The algo instance Id</param>
        /// <returns></returns>
        Task<ResponseModel> CancelLimitOrderAsync(Guid limitOrderId, string instanceId);
    }
}
