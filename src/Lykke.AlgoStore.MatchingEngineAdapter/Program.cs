using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Autofac;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services;
using Lykke.Common.Api.Contract.Responses;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;

namespace Lykke.AlgoStore.MatchingEngineAdapter
{
    internal sealed class Program
    {
        public static string EnvInfo => Environment.GetEnvironmentVariable("ENV_INFO");

        public static async Task Main(string[] args)
        {
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
                var startup = new Startup();

                startup.ConfigureServices(services);

                IsAliveCheck(startup);

                Console.ReadKey();
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

        private static void IsAliveCheck(Startup startup)
        {
            var healthService = startup.ApplicationContainer.Resolve<IHealthService>();
            var healthViloationMessage = healthService.GetHealthViolationMessage();

            if (healthViloationMessage != null)
            {
                Console.WriteLine($"Service is unhealthy: {healthViloationMessage}");
            }
            else
            {
                Console.WriteLine(
                    $"Name = {Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationName}");
                Console.WriteLine(
                    $"Version = {Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationVersion}");
                Console.WriteLine($"Env = {Program.EnvInfo}");
#if DEBUG
                Console.WriteLine("IsDebug = true");
#else
                    Console.WriteLine("IsDebug = false");
#endif
            }
        }
    }
}
