using System;
using Autofac;
using JetBrains.Annotations;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
using Lykke.AlgoStore.MatchingEngineAdapter.Settings;
using Lykke.Common.Log;
using Lykke.MatchingEngine.Connector.Services;
using Lykke.SettingsReader;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Modules
{
    public class MatchingEngineModule : Module
    {
        private readonly IReloadingManager<AppSettings> _settings;

        public MatchingEngineModule([NotNull] IReloadingManager<AppSettings> settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisgterMeClient(_settings.CurrentValue.MatchingEngineClient.IpEndpoint.GetClientIpEndPoint());
            
            builder.RegisterType<Services.MatchingEngineAdapter>()
                .As<IMatchingEngineAdapter>()
                .SingleInstance();
        }
    }
}
