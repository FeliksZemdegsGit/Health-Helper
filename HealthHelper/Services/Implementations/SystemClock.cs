using System;
using HealthHelper.Services.Contracts;

namespace HealthHelper.Services.Implementations;

public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;

    public DateOnly Today => DateOnly.FromDateTime(Now.LocalDateTime.Date);

    public bool IsWeekend => Today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}


