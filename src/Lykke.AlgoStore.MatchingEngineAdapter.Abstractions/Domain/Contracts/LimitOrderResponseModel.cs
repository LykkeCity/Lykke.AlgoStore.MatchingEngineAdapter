using ProtoBuf;
using System;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Contracts
{
    /// <summary>
    /// Response model for placing new limit orders.
    /// </summary>
    [ProtoContract]
    public class LimitOrderResponseModel
    {
        /// <summary>
        /// The identifier under which the limit order was placed.
        /// </summary>
        [ProtoMember(1, IsRequired = true)]
        public Guid Id { get; set; }
    }
}
