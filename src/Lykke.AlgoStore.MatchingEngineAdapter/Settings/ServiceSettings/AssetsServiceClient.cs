using Lykke.SettingsReader.Attributes;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Settings.ServiceSettings
{
    public class AssetsServiceClient
    {
        [HttpCheck("api/IsAlive")]
        public string ServiceUrl { get; set; }
    }
}
