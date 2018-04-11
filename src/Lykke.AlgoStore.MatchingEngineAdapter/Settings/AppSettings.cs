using JetBrains.Annotations;
using Lykke.AlgoStore.MatchingEngineAdapter.Settings.ServiceSettings;
using Lykke.AlgoStore.MatchingEngineAdapter.Settings.SlackNotifications;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Settings
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class AppSettings
    {
        public MatchingEngineAdapterSettings AlgoStoreMatchingEngineAdapter { get; set; }
        public SlackNotificationsSettings SlackNotifications { get; set; }
    }
}
