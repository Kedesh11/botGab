using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MawuGab.Core.Interfaces;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Security.Cryptography;

namespace MawuGab.Services;

public sealed class SftpUploader : ISftpUploader
{
    private readonly SftpOptions _options;
    private readonly ILogger<SftpUploader> _logger;

    public SftpUploader(IOptions<SftpOptions> options, ILogger<SftpUploader> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> UploadAsync(string localFilePath, string remotePath, CancellationToken ct)
    {
        try
        {
            using var client = CreateClient();
            client.Connect();
            using var fs = File.OpenRead(localFilePath);
            EnsureRemoteDirectory(client, Path.GetDirectoryName(remotePath)!.Replace('\\','/'));
            client.UploadFile(fs, remotePath.Replace('\\','/'));
            client.Disconnect();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SFTP upload failed for {file}", localFilePath);
            await Task.CompletedTask;
            return false;
        }
    }

    private SftpClient CreateClient()
    {
        AuthenticationMethod auth;
        if (!string.IsNullOrWhiteSpace(_options.PrivateKeyPath) && File.Exists(_options.PrivateKeyPath))
        {
            PrivateKeyFile pkf = string.IsNullOrWhiteSpace(_options.PrivateKeyPassphrase)
                ? new PrivateKeyFile(_options.PrivateKeyPath)
                : new PrivateKeyFile(_options.PrivateKeyPath, _options.PrivateKeyPassphrase);
            auth = new PrivateKeyAuthenticationMethod(_options.Username, pkf);
        }
        else
        {
            auth = new PasswordAuthenticationMethod(_options.Username, _options.Password);
        }

        var connInfo = new ConnectionInfo(_options.Host, _options.Port, _options.Username, auth);
        var client = new SftpClient(connInfo);
        if (_options.EnableHostKeyVerification)
        {
            client.HostKeyReceived += (s, e) =>
            {
                bool ok = false;
                if (!string.IsNullOrWhiteSpace(_options.FingerprintSha256))
                {
                    using var sha256 = SHA256.Create();
                    var hash = sha256.ComputeHash(e.HostKey);
                    var fp = Convert.ToBase64String(hash);
                    // OpenSSH SHA256 format is typically "SHA256:<base64>"; allow either
                    var expected = _options.FingerprintSha256.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase)
                        ? _options.FingerprintSha256.Substring(7)
                        : _options.FingerprintSha256;
                    ok = string.Equals(fp, expected, StringComparison.OrdinalIgnoreCase);
                }
                else if (!string.IsNullOrWhiteSpace(_options.Fingerprint))
                {
                    var fp = BitConverter.ToString(e.FingerPrint).Replace("-", ":");
                    ok = fp.Equals(_options.Fingerprint, StringComparison.OrdinalIgnoreCase);
                }
                e.CanTrust = ok;
            };
        }
        return client;
    }

    private void EnsureRemoteDirectory(SftpClient client, string dir)
    {
        var parts = dir.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = "/";
        foreach (var part in parts)
        {
            current = current.EndsWith('/') ? current + part : current + "/" + part;
            if (!client.Exists(current)) client.CreateDirectory(current);
        }
    }
}
