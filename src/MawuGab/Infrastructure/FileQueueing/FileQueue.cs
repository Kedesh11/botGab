using System.Text.Json;
using MawuGab.Core.Interfaces;

namespace MawuGab.Infrastructure.FileQueueing;

public sealed class FileQueue : IFileQueue
{
    private readonly string _queuePath;
    private readonly object _lock = new();

    public FileQueue(Microsoft.Extensions.Options.IOptions<MawuGab.Services.AgentOptions> options)
    {
        _queuePath = options.Value.QueuePath;
        Directory.CreateDirectory(_queuePath);
    }

    public string Enqueue(string filePath, IDictionary<string, string>? metadata = null)
    {
        var id = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(_queuePath);
        var destZip = Path.Combine(_queuePath, id + ".zip");
        // If file already in queue folder, just rename atomically; else move into queue
        var sameDir = string.Equals(Path.GetDirectoryName(filePath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            _queuePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        if (sameDir)
        {
            File.Move(filePath, destZip, overwrite: true);
        }
        else
        {
            File.Move(filePath, destZip, overwrite: true);
        }
        var metaObj = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdUtc"] = DateTime.UtcNow.ToString("O")
        };
        if (metadata != null)
        {
            foreach (var kv in metadata)
            {
                metaObj[kv.Key] = kv.Value;
            }
        }
        File.WriteAllText(Path.Combine(_queuePath, id + ".meta"), JsonSerializer.Serialize(metaObj));
        return id;
    }

    public string? Peek()
    {
        lock (_lock)
        {
            var pending = Directory.EnumerateFiles(_queuePath, "*.zip", SearchOption.TopDirectoryOnly)
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.CreationTimeUtc)
                .FirstOrDefault();
            return pending?.Name.Replace(".zip", string.Empty);
        }
    }

    public void MarkInProgress(string itemId)
    {
        var zip = Path.Combine(_queuePath, itemId + ".zip");
        var inprog = Path.Combine(_queuePath, itemId + ".inprogress");
        File.WriteAllText(inprog, DateTime.UtcNow.ToString("O"));
    }

    public void Complete(string itemId)
    {
        var zip = Path.Combine(_queuePath, itemId + ".zip");
        var meta = Path.Combine(_queuePath, itemId + ".meta");
        var inprog = Path.Combine(_queuePath, itemId + ".inprogress");
        SafeDelete(zip);
        SafeDelete(meta);
        SafeDelete(inprog);
    }

    public void Abandon(string itemId)
    {
        var inprog = Path.Combine(_queuePath, itemId + ".inprogress");
        SafeDelete(inprog);
        // Keep files to retry later
    }

    public int CountPending()
    {
        return Directory.EnumerateFiles(_queuePath, "*.zip").Count();
    }

    public IDictionary<string, string> GetMeta(string itemId)
    {
        var metaPath = Path.Combine(_queuePath, itemId + ".meta");
        if (!File.Exists(metaPath)) return new Dictionary<string, string>();
        try
        {
            var json = File.ReadAllText(metaPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            return dict;
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static void SafeDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* swallow */ }
    }
}
