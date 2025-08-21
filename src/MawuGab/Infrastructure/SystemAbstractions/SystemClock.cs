using MawuGab.Core.Interfaces;

namespace MawuGab.Infrastructure.SystemAbstractions;

public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
