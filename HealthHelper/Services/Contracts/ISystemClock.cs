using System;

namespace HealthHelper.Services.Contracts;

public interface ISystemClock
{
    DateTimeOffset Now { get; }
    DateOnly Today { get; }
    bool IsWeekend { get; }
}


