﻿using Autofac;
using Autofac.Extensions.DependencyInjection;
using AzureStorage.Tables;
using Common.Log;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Entities;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Repositories;
using Lykke.AlgoStore.Job.Stopping.Client;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Services;
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

            builder.RegisterType<HealthService>()
                .As<IHealthService>()
                .SingleInstance();

            builder.RegisterType<StartupManager>()
                .As<IStartupManager>();

            builder.RegisterType<ShutdownManager>()
                .As<IShutdownManager>();

            builder.RegisterType<ListeningService>()
                .As<IListeningService>()
                .WithParameter(TypedParameter.From(_settings.CurrentValue.AlgoStoreMatchingEngineAdapter.Listener.Port))
                .SingleInstance();

            builder.RegisterType<MessageHandler>()
                .As<IMessageHandler>()
                .SingleInstance();

            builder.RegisterInstance<IAlgoInstanceTradeRepository>(CreateAlgoTradeRepository(
                _settings.Nested(x => x.AlgoStoreMatchingEngineAdapter.Db.LogsConnectionString), _log)).SingleInstance();

            builder.RegisterInstance<IAlgoClientInstanceRepository>(CreateAlgoClientInstanceRepository(
                _settings.Nested(x => x.AlgoStoreMatchingEngineAdapter.Db.LogsConnectionString), _log)).SingleInstance();

            builder.RegisterAlgoInstanceStoppingClient(_settings.CurrentValue.AlgoStoreStoppingClient.ServiceUrl, _log);

            builder.Populate(_services);
        }

        private static AlgoInstanceTradeRepository CreateAlgoTradeRepository(IReloadingManager<string> connectionString,
            ILog log)
        {
            return new AlgoInstanceTradeRepository(
                AzureTableStorage<AlgoInstanceTradeEntity>.Create(connectionString, AlgoInstanceTradeRepository.TableName, log));
        }

        private static AlgoClientInstanceRepository CreateAlgoClientInstanceRepository(IReloadingManager<string> connectionString,
            ILog log)
        {
            return new AlgoClientInstanceRepository(
                AzureTableStorage<AlgoClientInstanceEntity>.Create(connectionString, AlgoClientInstanceRepository.TableName, log));
        }
    }
}
