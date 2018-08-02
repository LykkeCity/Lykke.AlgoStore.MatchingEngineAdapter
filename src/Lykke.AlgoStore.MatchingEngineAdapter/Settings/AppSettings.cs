using JetBrains.Annotations;
using Lykke.AlgoStore.Job.Stopping.Client;
using Lykke.AlgoStore.MatchingEngineAdapter.Settings.ServiceSettings;
using Lykke.AlgoStore.MatchingEngineAdapter.Settings.SlackNotifications;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Settings
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class AppSettings
    {
        public MatchingEngineAdapterSettings AlgoStoreMatchingEngineAdapter { get; set; }
        public MatchingEngineSettings MatchingEngineClient { get; set; }
        public SlackNotificationsSettings SlackNotifications { get; set; }
        public FeeCalculatorServiceClient FeeCalculatorServiceClient { get; set; }
        public AssetsServiceClient AssetsServiceClient { get; set; }
        public FeeSettings FeeSettings { get; set; }
        public AlgoStoreStoppingClientSettings AlgoStoreStoppingClient { get; set; }
    }
}
