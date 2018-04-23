using Autofac;
using System.Net;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Client
{
    public static class ContainerBuilderExtensions
    {
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
