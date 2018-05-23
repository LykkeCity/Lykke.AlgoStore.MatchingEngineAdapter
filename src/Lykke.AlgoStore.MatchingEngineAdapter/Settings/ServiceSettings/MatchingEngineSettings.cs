using System.Net;
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Settings.ServiceSettings
{
    public class MatchingEngineSettings
    {
        public IpEndpointSettings IpEndpoint { get; set; }
    }

    public class IpEndpointSettings
    {
        public string InternalHost { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }

        public async Task<IPEndPoint> GetClientIpEndPoint(bool useInternal = false)
        {
            string host = useInternal ? InternalHost : Host;

            if (IPAddress.TryParse(host, out var ipAddress))
                return new IPEndPoint(ipAddress, Port);

            var addresses = await Dns.GetHostAddressesAsync(host);
            return new IPEndPoint(addresses[0], Port);
        }
    }
}
