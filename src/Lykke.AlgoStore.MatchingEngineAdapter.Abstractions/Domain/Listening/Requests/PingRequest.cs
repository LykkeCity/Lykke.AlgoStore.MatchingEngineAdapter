using ProtoBuf;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests
{
    [ProtoContract]
    public class PingRequest
    {
        [ProtoMember(1, IsRequired = true)]
        public string Message { get; set; }
    }
}
