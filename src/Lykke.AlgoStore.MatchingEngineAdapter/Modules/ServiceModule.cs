using Autofac;
using Autofac.Extensions.DependencyInjection;
using Common.Log;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Repositories;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Settings;
using Lykke.SettingsReader;
using Microsoft.Extensions.DependencyInjection;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Modules
{
    public class ServiceModule : Module
    {
        private readonly IReloadingManager<AppSettings> _settings;
        private readonly ILog _log;
        // NOTE: you can remove it if you don't need to use IServiceCollection extensions to register service specific dependencies
        private readonly IServiceCollection _services;

        public ServiceModule(IReloadingManager<AppSettings> settings, ILog log)
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
                .WithParameter(TypedParameter.From(_settings.CurrentValue.AlgoStoreMatchingEngineAdapter.Listener.Port))
                .SingleInstance();

            builder.RegisterType<ProducerLoadBalancer>()
                .As<IProducerLoadBalancer>();

            builder.RegisterType<MessageQueue>()
                .As<IMessageQueue>()
                .SingleInstance();

            builder.RegisterType<AlgoInstanceTradeRepository>()
                .As<IAlgoInstanceTradeRepository>()
                .SingleInstance();

            // TODO: Add your dependencies here

            builder.Populate(_services);
        }
    }
}
