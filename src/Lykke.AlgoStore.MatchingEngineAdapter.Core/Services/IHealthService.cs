using System.Collections.Generic;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Domain.Health;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Core.Services
{
    // NOTE: See https://lykkex.atlassian.net/wiki/spaces/LKEWALLET/pages/35755585/Add+your+app+to+Monitoring
    public interface IHealthService
    {
        string GetHealthViolationMessage();
        IEnumerable<HealthIssue> GetHealthIssues();
    }
}
