using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using MawuGab.Core.Interfaces;

namespace MawuGab.Services;

public sealed class MetricsServer : IMetricsServer, IDisposable
{
    private readonly ILogger<MetricsServer> _logger;
    private HttpListener? _listener;
    private int _collected;
    private int _uploaded;
    private int _failed;
    private string _lastUpdate = "";
    private Func<int>? _pendingProvider;

    public MetricsServer(ILogger<MetricsServer> logger)
    {
        _logger = logger;
    }

    public void Start(int port)
    {
        _listener = new HttpListener();
        var prefix = $"http://localhost:{port}/metrics/";
        _listener.Prefixes.Add(prefix);
        try
        {
            _listener.Start();
            _ = Task.Run(Loop);
            _logger.LogInformation("Metrics server listening on {prefix}", prefix);
        }
        catch (HttpListenerException ex)
        {
            _logger.LogError(ex, "Failed to start metrics server on {prefix}", prefix);
        }
    }

    private async Task Loop()
    {
        if (_listener == null) return;
        while (_listener.IsListening)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                var payload = BuildMetrics();
                var buffer = Encoding.UTF8.GetBytes(payload);
                ctx.Response.ContentType = "text/plain; version=0.0.4";
                ctx.Response.ContentLength64 = buffer.Length;
                await ctx.Response.OutputStream.WriteAsync(buffer);
                ctx.Response.Close();
            }
            catch { /* ignore */ }
        }
    }

    private string BuildMetrics()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# HELP mawugab_collected_files Total collected .jrn files");
        sb.AppendLine("# TYPE mawugab_collected_files counter");
        sb.AppendLine($"mawugab_collected_files {_collected}");
        sb.AppendLine("# HELP mawugab_uploaded_files Total uploaded compressed files");
        sb.AppendLine("# TYPE mawugab_uploaded_files counter");
        sb.AppendLine($"mawugab_uploaded_files {_uploaded}");
        sb.AppendLine("# HELP mawugab_failed_queue Current failures pending in queue");
        sb.AppendLine("# TYPE mawugab_failed_queue gauge");
        var pending = _pendingProvider?.Invoke() ?? _failed;
        sb.AppendLine($"mawugab_failed_queue {pending}");
        sb.AppendLine("# HELP mawugab_last_update Last update version staged");
        sb.AppendLine("# TYPE mawugab_last_update gauge");
        if (!string.IsNullOrEmpty(_lastUpdate))
            sb.AppendLine($"mawugab_last_update{{version=\"{_lastUpdate}\"}} 1");
        else
            sb.AppendLine("mawugab_last_update 0");
        return sb.ToString();
    }

    public void IncrementCollected() => Interlocked.Increment(ref _collected);
    public void IncrementUploaded() => Interlocked.Increment(ref _uploaded);
    public void IncrementFailed() => Interlocked.Increment(ref _failed);
    public void SetLastUpdate(string version) => _lastUpdate = version;
    public int GetPendingCount() => _pendingProvider?.Invoke() ?? 0;

    public void Dispose()
    {
        try { _listener?.Stop(); _listener?.Close(); } catch { }
    }

    public void RegisterPendingProvider(Func<int> provider)
    {
        _pendingProvider = provider;
    }
}
