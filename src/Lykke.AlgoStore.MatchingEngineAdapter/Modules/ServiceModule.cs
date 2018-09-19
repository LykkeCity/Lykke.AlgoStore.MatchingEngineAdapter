using Autofac;
using Autofac.Extensions.DependencyInjection;
using AzureStorage.Tables;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Entities;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Repositories;
using Lykke.AlgoStore.Job.Stopping.Client;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Services;
using Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Settings;
using Lykke.Common.Log;
using Lykke.Logs;
using Lykke.Logs.Loggers.LykkeConsole;
using Lykke.Sdk.Health;
using Lykke.SettingsReader;
using Microsoft.Extensions.DependencyInjection;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Modules
{
    public class ServiceModule : Module
    {
        private readonly IReloadingManager<AppSettings> _settings;
        private readonly IServiceCollection _services;

        public ServiceModule(IReloadingManager<AppSettings> settings)
        {
            _settings = settings;

            _services = new ServiceCollection();
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<HealthService>()
                .As<IHealthService>()
                .SingleInstance();

            builder.RegisterType<StartupManager>()
                .As<IStartupManager>();

            builder.RegisterType<ShutdownManager>()
                .As<IShutdownManager>();

            RegisterFeeServices(builder);

            builder.RegisterType<ListeningService>()
                .As<IListeningService>()
                .WithParameter(TypedParameter.From(_settings.CurrentValue.AlgoStoreMatchingEngineAdapter.Listener.Port))
                .SingleInstance();

            builder.RegisterType<MessageHandler>()
                .As<IMessageHandler>()
                .SingleInstance();

            builder.Register(x =>
                {
                    var log = x.Resolve<ILogFactory>();
                    var repository = CreateAlgoTradeRepository(
                        _settings.Nested(y => y.AlgoStoreMatchingEngineAdapter.Db.LogsConnectionString), log);

                    return repository;
                })
                .As<IAlgoInstanceTradeRepository>()
                .SingleInstance();

            builder.Register(x =>
                {
                    var log = x.Resolve<ILogFactory>();
                    var repository = CreateAlgoClientInstanceRepository(
                        _settings.Nested(y => y.AlgoStoreMatchingEngineAdapter.Db.LogsConnectionString), log);

                    return repository;
                })
                .As<IAlgoClientInstanceRepository>()
                .SingleInstance();

            var logFactory = LogFactory.Create().AddConsole();
            builder.RegisterAlgoInstanceStoppingClient(_settings.CurrentValue.AlgoStoreStoppingClient.ServiceUrl, logFactory.CreateLog(this));

            builder.Populate(_services);
        }

        private void RegisterFeeServices(ContainerBuilder builder)
        {
            builder.RegisterType<FeeCalculatorAdapter>()
                .As<IFeeCalculatorAdapter>()
                .WithParameter(TypedParameter.From(_settings.CurrentValue.FeeSettings.TargetClientId.Hft))
                .SingleInstance();
        }

        private static AlgoInstanceTradeRepository CreateAlgoTradeRepository(IReloadingManager<string> connectionString,
            ILogFactory log)
        {
            return new AlgoInstanceTradeRepository(
                AzureTableStorage<AlgoInstanceTradeEntity>.Create(connectionString, AlgoInstanceTradeRepository.TableName, log));
        }

        private static AlgoClientInstanceRepository CreateAlgoClientInstanceRepository(IReloadingManager<string> connectionString,
            ILogFactory log)
        {
            return new AlgoClientInstanceRepository(
                AzureTableStorage<AlgoClientInstanceEntity>.Create(connectionString, AlgoClientInstanceRepository.TableName, log),
                AzureTableStorage<AlgoInstanceStoppingEntity>.Create(connectionString, AlgoClientInstanceRepository.TableName, log),
                AzureTableStorage<AlgoInstanceTcBuildEntity>.Create(connectionString, AlgoClientInstanceRepository.TableName, log));
        }
    }
}
