using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MawuGab.Core.Interfaces;
using MawuGab.Services;

namespace MawuGab;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ILogCollector _collector;
    private readonly ICompressor _compressor;
    private readonly IFileQueue _queue;
    private readonly ISftpUploader _uploader;
    private readonly IUpdateManager _updater;
    private readonly IMetricsServer _metrics;
    private readonly AgentOptions _options;
    private readonly SftpOptions _sftpOptions;
    private readonly UpdateOptions _updateOptions;

    public Worker(
        ILogger<Worker> logger,
        ILogCollector collector,
        ICompressor compressor,
        IFileQueue queue,
        ISftpUploader uploader,
        IUpdateManager updater,
        IMetricsServer metrics,
        IOptions<AgentOptions> options,
        IOptions<SftpOptions> sftpOptions,
        IOptions<UpdateOptions> updateOptions)
    {
        _logger = logger;
        _collector = collector;
        _compressor = compressor;
        _queue = queue;
        _uploader = uploader;
        _updater = updater;
        _metrics = metrics;
        _options = options.Value;
        _sftpOptions = sftpOptions.Value;
        _updateOptions = updateOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _metrics.Start(_options.MetricsPort);
        _metrics.RegisterPendingProvider(() => _queue.CountPending());

        _ = Task.Run(() => UpdateLoop(stoppingToken), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Discover and enqueue new logs
                foreach (var log in _collector.DiscoverNewLogs())
                {
                    var compressed = _compressor.CompressFile(log.FullPath, _options.QueuePath);
                    _metrics.IncrementCollected();
                    _queue.Enqueue(compressed, new Dictionary<string, string>
                    {
                        ["bank"] = log.BankName,
                        ["gab"] = log.GabId,
                        ["original"] = log.FileName
                    });
                    // Move original .jrn to processed folder to prevent re-collection
                    try
                    {
                        var destDir = Path.Combine(_options.ProcessedPath, log.BankName, log.GabId);
                        Directory.CreateDirectory(destDir);
                        var destFile = Path.Combine(destDir, log.FileName);
                        if (File.Exists(destFile))
                        {
                            // If exists, append timestamp to avoid collision
                            var name = Path.GetFileNameWithoutExtension(log.FileName);
                            var ext = Path.GetExtension(log.FileName);
                            destFile = Path.Combine(destDir, $"{name}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}");
                        }
                        File.Move(log.FullPath, destFile, overwrite: false);
                    }
                    catch (Exception moveEx)
                    {
                        _logger.LogWarning(moveEx, "Failed to move processed log {file} to archive", log.FullPath);
                    }
                }

                // Retry uploads
                var item = _queue.Peek();
                if (item != null)
                {
                    _queue.MarkInProgress(item);
                    var zipPath = Path.Combine(_options.QueuePath, item + ".zip");
                    var remote = BuildRemotePath(item, zipPath);
                    var ok = await _uploader.UploadAsync(zipPath, remote, stoppingToken);
                    if (ok)
                    {
                        _queue.Complete(item);
                        _metrics.IncrementUploaded();
                    }
                    else
                    {
                        _queue.Abandon(item);
                        _metrics.IncrementFailed();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop error");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.RetryIntervalSeconds), stoppingToken);
        }
    }

    private string BuildRemotePath(string itemId, string localZip)
    {
        var fileName = Path.GetFileName(localZip);
        var meta = _queue.GetMeta(itemId);
        var bank = meta.TryGetValue("bank", out var b) ? b : (_options.BankName ?? "UNKNOWN");
        var gab = meta.TryGetValue("gab", out var g) ? g : (_options.GabId ?? "UNKNOWN");
        var basePath = _sftpOptions.RemoteBasePath.Trim('/');
        return $"/{basePath}/{bank}/{gab}/{fileName}";
    }

    private async Task UpdateLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await _updater.CheckAndApplyUpdatesAsync(ct);
            await Task.Delay(TimeSpan.FromMinutes(_updateOptions.CheckIntervalMinutes), ct);
        }
    }
}
