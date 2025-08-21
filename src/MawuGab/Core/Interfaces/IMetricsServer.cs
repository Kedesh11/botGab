namespace MawuGab.Core.Interfaces;

public interface IMetricsServer
{
    void Start(int port);
    void IncrementCollected();
    void IncrementUploaded();
    void IncrementFailed();
    void SetLastUpdate(string version);
    int GetPendingCount();
    void RegisterPendingProvider(Func<int> provider);
}
