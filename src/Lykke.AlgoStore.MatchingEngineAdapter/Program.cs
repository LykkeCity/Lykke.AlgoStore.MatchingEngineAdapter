using System;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;

namespace Lykke.AlgoStore.MatchingEngineAdapter
{
    internal sealed class Program
    {
        private static Startup _startup;

        public static string EnvInfo => Environment.GetEnvironmentVariable("ENV_INFO");

        public static async Task Main(string[] args)
        {
            AssemblyLoadContext.Default.Unloading += Application_Shutdown;

            Console.WriteLine(
                $"{PlatformServices.Default.Application.ApplicationName} version {PlatformServices.Default.Application.ApplicationVersion}");
#if DEBUG
            Console.WriteLine("Is DEBUG");
#else
            Console.WriteLine("Is RELEASE");
#endif
            Console.WriteLine($"ENV_INFO: {EnvInfo}");

            try
            {
                var services = new ServiceCollection();
                _startup = new Startup();

                _startup.ConfigureServices(services);

                _startup.Log.WriteMonitorAsync("", $"Env: {EnvInfo}", "Started").Wait();

                IsAliveCheck();

                // Start the listening service
                var listeningService = _startup.ApplicationContainer.Resolve<IListeningService>();
                listeningService.Start();

                Console.In.ReadLineAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fatal error:");
                Console.WriteLine(ex);

                // Lets devops to see startup error in console between restarts in the Kubernetes
                var delay = TimeSpan.FromMinutes(1);

                Console.WriteLine();
                Console.WriteLine($"Process will be terminated in {delay}. Press any key to terminate immediately.");

                await Task.WhenAny(
                    Task.Delay(delay),
                    Task.Run(() => { Console.ReadKey(true); }));
            }

            Console.WriteLine("Terminated");
        }

        private static void Application_Shutdown(AssemblyLoadContext obj)
        {
            _startup.Log.WriteMonitorAsync("", $"Env: {EnvInfo}", "Closed").Wait();

            //Need to sleep for 5s in order for log to get saved
            Thread.Sleep(5000);
        }

        private static void IsAliveCheck()
        {
            var healthService = _startup.ApplicationContainer.Resolve<IHealthService>();
            var healthViloationMessage = healthService.GetHealthViolationMessage();

            if (healthViloationMessage != null)
            {
                Console.WriteLine($"Service is unhealthy: {healthViloationMessage}");
            }
            else
            {
                Console.WriteLine(
                    $"Name = {PlatformServices.Default.Application.ApplicationName}");

                Console.WriteLine(
                    $"Version = {PlatformServices.Default.Application.ApplicationVersion}");

                Console.WriteLine($"Env = {EnvInfo}");
#if DEBUG
                Console.WriteLine("IsDebug = true");
#else
                    Console.WriteLine("IsDebug = false");
#endif
            }
        }
    }
}
