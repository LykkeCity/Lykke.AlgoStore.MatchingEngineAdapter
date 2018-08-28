using Autofac;
using Autofac.Extensions.DependencyInjection;
using AzureStorage.Tables;
using Common.Log;
using Lykke.AlgoStore.MatchingEngineAdapter.Modules;
using Lykke.AlgoStore.MatchingEngineAdapter.Settings;
using Lykke.Logs;
using Lykke.SettingsReader;
using Lykke.SlackNotification.AzureQueue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using AutoMapper;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Mapper;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
using Lykke.Common.ApiLibrary.Middleware;
using Lykke.Common.ApiLibrary.Swagger;
using Lykke.Common.Log;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Lykke.AlgoStore.MatchingEngineAdapter
{
    public class Startup
    {
        public IHostingEnvironment Environment { get; }
        public IContainer ApplicationContainer { get; private set; }
        public IConfigurationRoot Configuration { get; }
        private ILog _log;

        public Startup(IHostingEnvironment env)
        {
            InitMapper();

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddEnvironmentVariables();

            Configuration = builder.Build();

            Environment = env;
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            try
            {
                services.AddMvc()
                    .AddJsonOptions(options =>
                    {
                        options.SerializerSettings.ContractResolver =
                            new Newtonsoft.Json.Serialization.DefaultContractResolver();
                    });

                services.AddSwaggerGen(options =>
                {
                    options.DefaultLykkeConfiguration("v1", "Lykke.AlgoStore.MathcingEngineAdapter API");
                });

                var settingsManager = Configuration.LoadSettings<AppSettings>(x => (
                    x.SlackNotifications.AzureQueue.ConnectionString, x.SlackNotifications.AzureQueue.QueueName,
                    $"MatchingEngineAdapter"));

                var appSettings = settingsManager;

                services.AddLykkeLogging(
                    settingsManager.ConnectionString(s => s.AlgoStoreMatchingEngineAdapter.Db.LogsConnectionString),
                    "MatchingEngineAdapterLog",
                    appSettings.CurrentValue.SlackNotifications.AzureQueue.ConnectionString,
                    appSettings.CurrentValue.SlackNotifications.AzureQueue.QueueName);

                var builder = new ContainerBuilder();

                //Log = CreateLogWithSlack(services, appSettings);

                //var logFactory = ApplicationContainer.Resolve<ILogFactory>();
                //Log = logFactory.CreateLog(this);

                builder.RegisterModule(new ServiceModule(appSettings));
                builder.RegisterModule(new ClientsModule(appSettings));
                builder.RegisterModule(new MatchingEngineModule(appSettings));

                builder.Populate(services);
                ApplicationContainer = builder.Build();

                var logFactory = ApplicationContainer.Resolve<ILogFactory>();
                _log = logFactory.CreateLog(this);

                return new AutofacServiceProvider(ApplicationContainer);
            }
            catch (Exception ex)
            {
                _log?.WriteFatalError(nameof(Startup), nameof(ConfigureServices), ex);
                throw;
            }
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime appLifetime)
        {
            try
            {
                if (env.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }

                app.UseLykkeForwardedHeaders();
                app.UseLykkeMiddleware(ex => new { Message = "Technical problem" });

                app.UseMvc();
                app.UseSwagger(c =>
                {
                    c.PreSerializeFilters.Add((swagger, httpReq) => swagger.Host = httpReq.Host.Value);
                });
                app.UseSwaggerUI(x =>
                {
                    x.RoutePrefix = "swagger/ui";
                    x.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                });
                app.UseStaticFiles();

                appLifetime.ApplicationStarted.Register(() => StartApplication().GetAwaiter().GetResult());
                appLifetime.ApplicationStopping.Register(() => StopApplication().GetAwaiter().GetResult());
                appLifetime.ApplicationStopped.Register(() => CleanUp().GetAwaiter().GetResult());
            }
            catch (Exception ex)
            {
                _log?.WriteFatalError(nameof(Startup), nameof(Configure), ex);
                throw;
            }
        }

        private static void InitMapper()
        {
            Mapper.Initialize(cfg =>
            {
                cfg.AddProfiles(typeof(AutoMapperModelProfile));
            });

            Mapper.AssertConfigurationIsValid();
        }

        private async Task StartApplication()
        {
            try
            {
                // NOTE: Service not yet receive and process requests here

                await ApplicationContainer.Resolve<IStartupManager>().StartAsync();

                await _log.WriteMonitorAsync("", $"Env: {Program.EnvInfo}", "Started");

                //Let us start our listener here
                var listeningService = ApplicationContainer.Resolve<IListeningService>();
                var listeningServiceTask = listeningService.Start();

                // Intentionally disabled unawaited task warning here
#pragma warning disable 4014
                listeningServiceTask.ContinueWith((task) =>
                {
                    if (task.IsFaulted)
                        _log.WriteFatalError("", nameof(listeningService), task.Exception);
                });
#pragma warning restore 4014

                await _log.WriteMonitorAsync("", $"{nameof(listeningService)}", "Started");
            }
            catch (Exception ex)
            {
                await _log.WriteFatalErrorAsync(nameof(Startup), nameof(StartApplication), "", ex);
                throw;
            }
        }

        private async Task StopApplication()
        {
            try
            {
                // NOTE: Service still can receive and process requests here, so take care about it if you add logic here.

                await ApplicationContainer.Resolve<IShutdownManager>().StopAsync();
            }
            catch (Exception ex)
            {
                if (_log != null)
                {
                    await _log.WriteFatalErrorAsync(nameof(Startup), nameof(StopApplication), "", ex);
                }

                throw;
            }
        }

        private async Task CleanUp()
        {
            try
            {
                // NOTE: Service can't receive and process requests here, so you can destroy all resources

                if (_log != null)
                {
                    await _log.WriteMonitorAsync("", $"Env: {Program.EnvInfo}", "Terminating");
                }

                ApplicationContainer.Dispose();
            }
            catch (Exception ex)
            {
                if (_log != null)
                {
                    await _log.WriteFatalErrorAsync(nameof(Startup), nameof(CleanUp), "", ex);
                    (_log as IDisposable)?.Dispose();
                }

                throw;
            }
        }

        private static ILog CreateLogWithSlack(IServiceCollection services, IReloadingManager<AppSettings> settings)
        {
            var consoleLogger = new LogToConsole();
            var aggregateLogger = new AggregateLogger();

            aggregateLogger.AddLog(consoleLogger);

            var dbLogConnectionStringManager =
                settings.Nested(x => x.AlgoStoreMatchingEngineAdapter.Db.LogsConnectionString);
            var dbLogConnectionString = dbLogConnectionStringManager.CurrentValue;

            if (string.IsNullOrEmpty(dbLogConnectionString))
            {
                consoleLogger
                    .WriteWarningAsync(nameof(Startup), nameof(CreateLogWithSlack), "Table logger is not initiated")
                    .Wait();
                return aggregateLogger;
            }

            if (dbLogConnectionString.StartsWith("${") && dbLogConnectionString.EndsWith("}"))
                throw new InvalidOperationException(
                    $"LogsConnString {dbLogConnectionString} is not filled in settings");

            var persistenceManager = new LykkeLogToAzureStoragePersistenceManager(
                AzureTableStorage<LogEntity>.Create(dbLogConnectionStringManager, "MatchingEngineAdapterLog",
                    consoleLogger),
                consoleLogger);

            // Creating slack notification service, which logs own azure queue processing messages to aggregate log
            var slackService = services.UseSlackNotificationsSenderViaAzureQueue(
                new AzureQueueIntegration.AzureQueueSettings
                {
                    ConnectionString = settings.CurrentValue.SlackNotifications.AzureQueue.ConnectionString,
                    QueueName = settings.CurrentValue.SlackNotifications.AzureQueue.QueueName
                }, aggregateLogger);

            var slackNotificationsManager = new LykkeLogToAzureSlackNotificationsManager(slackService, consoleLogger);

            // Creating azure storage logger, which logs own messages to console log
            var azureStorageLogger = new LykkeLogToAzureStorage(
                persistenceManager,
                slackNotificationsManager,
                consoleLogger);

            azureStorageLogger.Start();

            aggregateLogger.AddLog(azureStorageLogger);

            return aggregateLogger;
        }
    }
}
