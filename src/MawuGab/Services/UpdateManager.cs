using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MawuGab.Core.Interfaces;

namespace MawuGab.Services;

public sealed class UpdateManager : IUpdateManager
{
    private readonly UpdateOptions _options;
    private readonly ILogger<UpdateManager> _logger;
    private readonly IMetricsServer _metrics;

    public UpdateManager(IOptions<UpdateOptions> options, ILogger<UpdateManager> logger, IMetricsServer metrics)
    {
        _options = options.Value;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task CheckAndApplyUpdatesAsync(CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient();
            var manifest = await http.GetFromJsonAsync<Manifest>(_options.ManifestUrl, ct);
            if (manifest is null) return;

            var currentVersion = typeof(UpdateManager).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            if (string.Equals(currentVersion, manifest.version, StringComparison.OrdinalIgnoreCase)) return;

            Directory.CreateDirectory(_options.UpdatesPath);
            var pkgUrl = _options.DownloadBaseUrl.TrimEnd('/') + "/" + manifest.package;
            var tempFile = Path.Combine(_options.UpdatesPath, manifest.package);
            await using (var s = await http.GetStreamAsync(pkgUrl, ct))
            await using (var fs = File.Create(tempFile))
                await s.CopyToAsync(fs, ct);

            // For safety in this reference implementation, we only stage the update and expose metric.
            // Real deployment can integrate a side-by-side replace + service restart via SCM.
            _metrics.SetLastUpdate(manifest.version);
            _logger.LogInformation("Update {version} downloaded to {path}. Apply step is organization-specific.", manifest.version, tempFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check/apply failed");
        }
    }

    private sealed record Manifest(string version, string package);
}
