using Autofac;
using Autofac.Extensions.DependencyInjection;
using Common.Log;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Settings.ServiceSettings;
using Lykke.SettingsReader;
using Microsoft.Extensions.DependencyInjection;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Modules
{
    public class ServiceModule : Module
    {
        private readonly IReloadingManager<MatchingEngineAdapterSettings> _settings;
        private readonly ILog _log;
        // NOTE: you can remove it if you don't need to use IServiceCollection extensions to register service specific dependencies
        private readonly IServiceCollection _services;

        public ServiceModule(IReloadingManager<MatchingEngineAdapterSettings> settings, ILog log)
        {
            _settings = settings;
            _log = log;

            _services = new ServiceCollection();
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInstance(_log)
                    .As<ILog>()
                    .SingleInstance();

            builder.RegisterType<ListeningService>()
                .As<IListeningService>()
                .WithParameter(TypedParameter.From(_settings.CurrentValue.Listener.Port))
                .SingleInstance();

            builder.RegisterType<ProducerLoadBalancer>()
                .As<IProducerLoadBalancer>();

            builder.RegisterType<RequestQueue>()
                .As<IRequestQueue>()
                .SingleInstance();

            builder.Populate(_services);
        }
    }
}
