using System;

namespace HealthHelper.Models;

public record SleepLog(DateTimeOffset BedTime, DateTimeOffset WakeTime, int QualityScore)
{
    public TimeSpan Duration => WakeTime - BedTime;
}

public record HydrationLog(double TargetMl, double ConsumedMl)
{
    public double RemainingMl => Math.Max(0, TargetMl - ConsumedMl);
    public bool IsGoalMet => ConsumedMl >= TargetMl;
}

public record ActivityLog(int WorkoutMinutes, int SedentaryMinutes);

public record DailySnapshot(
    DateOnly Date,
    SleepLog? Sleep,
    HydrationLog? Hydration,
    ActivityLog? Activity,
    AdviceBundle? Advice = null);

public record HealthTip(
    int Id,
    string Title,
    string Content,
    string Category,
    DateTimeOffset CreatedAt);

public record FavoriteTip(
    int TipId,
    DateTimeOffset FavoritedAt);


