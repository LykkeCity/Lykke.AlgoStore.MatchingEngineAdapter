using Autofac;
using System.Net;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    /// <summary>
    /// Provides <see cref="ContainerBuilder"/> extensions for registering the MEA client services
    /// </summary>
    public static class ContainerBuilderExtensions
    {
        /// <summary>
        /// Registers the MEA client services to a given <see cref="ContainerBuilder"/>
        /// </summary>
        /// <param name="builder">The <see cref="ContainerBuilder"/> to register the services in</param>
        /// <param name="endPoint">The IP address of the matching engine adapter</param>
        /// <param name="port">The port of the matching engine adapter</param>
        public static void RegisterMatchingEngineClient(this ContainerBuilder builder, IPAddress endPoint, ushort port)
        {
            builder.RegisterType<MeaCommunicator>()
                   .As<IMeaCommunicator>()
                   .WithParameter(TypedParameter.From(endPoint))
                   .WithParameter(TypedParameter.From(port))
                   .SingleInstance();

            builder.RegisterType<RequestManager>()
                   .As<IRequestManager>()
                   .SingleInstance();

            builder.RegisterType<MatchingEngineAdapterClient>()
                   .As<IMatchingEngineAdapterClient>()
                   .SingleInstance();
        }
    }
}
