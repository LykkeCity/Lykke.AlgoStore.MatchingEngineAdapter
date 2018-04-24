namespace Lykke.AlgoStore.MatchingEngineAdapter.Settings.ServiceSettings
{
    public class FeeSettings
    {
        public TargetClientIdFeeSettings TargetClientId { get; set; }

        public class TargetClientIdFeeSettings
        {
            public string Hft { get; set; }
        }
    }
}
