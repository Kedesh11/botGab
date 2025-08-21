namespace MawuGab.Core.Interfaces;

public interface IFileQueue
{
    string Enqueue(string filePath, IDictionary<string, string>? metadata = null);
    string? Peek();
    void MarkInProgress(string itemId);
    void Complete(string itemId);
    void Abandon(string itemId);
    int CountPending();
    IDictionary<string, string> GetMeta(string itemId);
}
