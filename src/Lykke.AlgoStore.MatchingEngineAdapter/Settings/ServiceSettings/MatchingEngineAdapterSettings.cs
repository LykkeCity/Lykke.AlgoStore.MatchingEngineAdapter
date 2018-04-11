using JetBrains.Annotations;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Settings.ServiceSettings
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class MatchingEngineAdapterSettings
    {
        public DbSettings Db { get; set; }
    }
}
