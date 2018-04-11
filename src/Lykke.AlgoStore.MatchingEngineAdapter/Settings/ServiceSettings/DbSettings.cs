﻿using Lykke.SettingsReader.Attributes;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Settings.ServiceSettings
{
    public class DbSettings
    {
        [AzureTableCheck]
        public string LogsConnectionString { get; set; }
    }
}
