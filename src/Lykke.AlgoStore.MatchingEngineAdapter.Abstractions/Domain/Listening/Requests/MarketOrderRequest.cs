using ProtoBuf;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests
{
    [ProtoContract]
    public class MarketOrderRequest
    {
        [ProtoMember(1, IsRequired = true)]
        public string ClientId { get; set; }

        [ProtoMember(2, IsRequired = true)]
        public string AssetPairId { get; set; }

        [ProtoMember(3, IsRequired = true)]
        public OrderAction OrderAction { get; set; }

        [ProtoMember(4, IsRequired = true)]
        public double Volume { get; set; }

        [ProtoMember(5, IsRequired = true)]
        public bool IsStraight { get; set; }

        [ProtoMember(6, IsRequired = true)]
        public string InstanceId { get; set; }
    }
}
