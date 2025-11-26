using System.Threading;
using System.Threading.Tasks;
using HealthHelper.Models;

namespace HealthHelper.Services.Contracts;

public interface IHealthInsightsService
{
    Task SaveSnapshotAsync(DailySnapshot snapshot, CancellationToken cancellationToken = default);
    Task<int> GetStoredDayCountAsync(CancellationToken cancellationToken = default);
    Task<AdviceBundle> GenerateCombinedAdviceAsync(DailySnapshot snapshot, CancellationToken cancellationToken = default);
}

