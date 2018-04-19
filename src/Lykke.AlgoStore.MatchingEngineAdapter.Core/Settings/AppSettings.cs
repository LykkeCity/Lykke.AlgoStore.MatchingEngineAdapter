using JetBrains.Annotations;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Settings.ServiceSettings;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Settings.SlackNotifications;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Settings
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class AppSettings
    {
        public MatchingEngineAdapterSettings AlgoStoreMatchingEngineAdapter { get; set; }
        public MatchingEngineSettings MatchingEngineClient { get; set; }
        public SlackNotificationsSettings SlackNotifications { get; set; }
        public FeeCalculatorServiceClient FeeCalculatorServiceClient { get; set; }
        public FeeSettings FeeSettings { get; set; }
    }
}
