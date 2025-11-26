using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthHelper.Models;

namespace HealthHelper.Services.Contracts;

public interface IRecommendationClient
{
    Task<string> CreateNarrativeAsync(
        DailySnapshot today,
        IReadOnlyList<DailySnapshot> historicalSnapshots,
        CancellationToken cancellationToken = default);
}

