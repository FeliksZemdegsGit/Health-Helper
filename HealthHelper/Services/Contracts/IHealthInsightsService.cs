using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthHelper.Models;

namespace HealthHelper.Services.Contracts;

public interface IHealthInsightsService
{
    Task SaveSnapshotAsync(DailySnapshot snapshot, CancellationToken cancellationToken = default);
    Task<int> GetStoredDayCountAsync(CancellationToken cancellationToken = default);
    Task<AdviceBundle> GenerateCombinedAdviceAsync(DailySnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailySnapshot>> GetHistoricalSnapshotsAsync(int days = 7, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailySnapshot>> GetHistoricalSnapshotsPagedAsync(int pageSize, int skip, CancellationToken cancellationToken = default);

    // Health Tips methods
    Task<HealthTip> GetRandomHealthTipAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HealthTip>> GetAllHealthTipsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsTipFavoritedAsync(int tipId, CancellationToken cancellationToken = default);
    Task ToggleFavoriteAsync(int tipId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HealthTip>> GetFavoritedTipsAsync(CancellationToken cancellationToken = default);
}

