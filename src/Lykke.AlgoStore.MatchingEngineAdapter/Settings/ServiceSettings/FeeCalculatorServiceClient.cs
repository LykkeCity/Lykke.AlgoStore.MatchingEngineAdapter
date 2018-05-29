using Lykke.SettingsReader.Attributes;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Settings.ServiceSettings
{
    public class FeeCalculatorServiceClient
    {
        [HttpCheck("api/IsAlive")]
        public string ServiceUrl { get; set; }
    }
}
