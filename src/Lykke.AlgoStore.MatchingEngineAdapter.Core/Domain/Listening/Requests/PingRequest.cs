using ProtoBuf;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Listening.Requests
{
    [ProtoContract]
    public class PingRequest
    {
        [ProtoMember(1, IsRequired = true)]
        public string Message { get; set; }
    }
}
