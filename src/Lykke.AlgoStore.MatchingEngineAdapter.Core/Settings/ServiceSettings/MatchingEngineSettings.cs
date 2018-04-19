using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Settings.ServiceSettings
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

        public IPEndPoint GetClientIpEndPoint(bool useInternal = false)
        {
            string host = useInternal ? InternalHost : Host;

            if (IPAddress.TryParse(host, out var ipAddress))
                return new IPEndPoint(ipAddress, Port);

            var addresses = Dns.GetHostAddressesAsync(host).Result;
            return new IPEndPoint(addresses[0], Port);
        }
    }
}
