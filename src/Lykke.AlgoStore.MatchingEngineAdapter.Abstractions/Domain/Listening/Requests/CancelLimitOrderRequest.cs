using ProtoBuf;
using System;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests
{
    [ProtoContract]
    public class CancelLimitOrderRequest
    {
        [ProtoMember(1, IsRequired = true)]
        public Guid LimitOrderId { get; set; }

        [ProtoMember(2, IsRequired = true)]
        public string InstanceId { get; set; }
    }
}
