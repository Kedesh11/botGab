using Microsoft.Extensions.Logging;
using MawuGab.Core.Interfaces;

namespace MawuGab.Services;

public sealed class NoopSftpUploader : ISftpUploader
{
    private readonly ILogger<NoopSftpUploader> _logger;
    public NoopSftpUploader(ILogger<NoopSftpUploader> logger)
    {
        _logger = logger;
    }

    public Task<bool> UploadAsync(string localFilePath, string remotePath, CancellationToken ct)
    {
        _logger.LogWarning("NoopSftpUploader actif (build non-Windows) — upload simulé: {file} -> {remote}", localFilePath, remotePath);
        return Task.FromResult(true);
    }
}
