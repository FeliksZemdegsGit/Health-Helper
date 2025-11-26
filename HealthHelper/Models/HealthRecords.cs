using System;

namespace HealthHelper.Models;

public record SleepLog(DateTimeOffset BedTime, DateTimeOffset WakeTime, int QualityScore)
{
    public TimeSpan Duration => WakeTime - BedTime;
}

public record HydrationLog(double TargetMl, double ConsumedMl, double BodyWeightKg)
{
    public double RemainingMl => Math.Max(0, TargetMl - ConsumedMl);
}

public record ActivityLog(int WorkoutMinutes, int SedentaryMinutes);

public record DailySnapshot(
    DateOnly Date,
    double BodyWeightKg,
    SleepLog? Sleep,
    HydrationLog? Hydration,
    ActivityLog? Activity);


