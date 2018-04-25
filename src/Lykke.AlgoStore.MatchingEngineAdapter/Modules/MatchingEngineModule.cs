using Autofac;
using Common.Log;
using JetBrains.Annotations;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services;
using Lykke.AlgoStore.MatchingEngineAdapter.Settings;
using Lykke.SettingsReader;
using System;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Modules
{
    public class MatchingEngineModule : Module
    {
        private readonly IReloadingManager<AppSettings> _settings;
        private readonly ILog _log;

        public MatchingEngineModule(
            [NotNull] IReloadingManager<AppSettings> settings,
            [NotNull] ILog log)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.BindMeClient(_settings.CurrentValue.MatchingEngineClient.IpEndpoint.GetClientIpEndPoint(), socketLog: null, ignoreErrors: true);

            builder.RegisterType<Abstractions.MatchingEngineAdapter>()
                .As<IMatchingEngineAdapter>()
                .WithParameter(TypedParameter.From(_settings.CurrentValue.FeeSettings.TargetClientId.Hft))
                .SingleInstance();
        }
    }
}
