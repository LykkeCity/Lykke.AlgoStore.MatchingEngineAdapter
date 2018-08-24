using Lykke.SettingsReader.Attributes;
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
        [TcpCheck("Port")]
        public string Host { get; set; }
        public int Port { get; set; }

        public IPEndPoint GetClientIpEndPoint()
        {
            if (IPAddress.TryParse(Host, out var ipAddress))
                return new IPEndPoint(ipAddress, Port);

            var addresses = Dns.GetHostAddressesAsync(Host).Result;
            return new IPEndPoint(addresses[0], Port);
        }
    }
}
