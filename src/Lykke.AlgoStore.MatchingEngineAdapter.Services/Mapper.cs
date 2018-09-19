using System;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain;
using FeeOrderAction = Lykke.Service.FeeCalculator.AutorestClient.Models.OrderAction;
using MeCommon = Lykke.MatchingEngine.Connector.Models.Common;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services
{
    public static class Mapper
    {
        public static FeeOrderAction ToFeeOrderAction(this OrderAction action)
        {
            FeeOrderAction orderAction;
            switch (action)
            {
                case OrderAction.Buy:
                    orderAction = FeeOrderAction.Buy;
                    break;
                case OrderAction.Sell:
                    orderAction = FeeOrderAction.Sell;
                    break;
                default:
                    throw new Exception("Unknown order action");
            }

            return orderAction;
        }

        public static MeCommon.OrderAction ToMeOrderAction(this OrderAction action)
        {
            MeCommon.OrderAction orderAction;
            switch (action)
            {
                case OrderAction.Buy:
                    orderAction = MeCommon.OrderAction.Buy;
                    break;
                case OrderAction.Sell:
                    orderAction = MeCommon.OrderAction.Sell;
                    break;
                default:
                    throw new Exception("Unknown order action");
            }

            return orderAction;
        }
    }
}
