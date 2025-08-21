namespace MawuGab.Core.Interfaces;

public interface IUpdateManager
{
    Task CheckAndApplyUpdatesAsync(CancellationToken ct);
}
