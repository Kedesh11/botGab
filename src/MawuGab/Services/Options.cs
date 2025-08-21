namespace MawuGab.Services;

public sealed class AgentOptions
{
    public string LogSourcePath { get; set; } = "C:/BankLogs";
    public string LogsPath { get; set; } = "C:/ProgramData/MawuGab/logs";
    public string QueuePath { get; set; } = "C:/ProgramData/MawuGab/queue";
    public string ProcessedPath { get; set; } = "C:/ProgramData/MawuGab/processed";
    public int ScanIntervalSeconds { get; set; } = 60;
    public int RetryIntervalSeconds { get; set; } = 60;
    public int MetricsPort { get; set; } = 9090;
    public string? BankName { get; set; }
    public string? GabId { get; set; }
}

public sealed class SftpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string RemoteBasePath { get; set; } = "/data";
    public string? Fingerprint { get; set; }
    public bool EnableHostKeyVerification { get; set; } = false;
    public string? FingerprintSha256 { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? PrivateKeyPassphrase { get; set; }
}

public sealed class UpdateOptions
{
    public int CheckIntervalMinutes { get; set; } = 30;
    public string ManifestUrl { get; set; } = string.Empty;
    public string DownloadBaseUrl { get; set; } = string.Empty;
    public string UpdatesPath { get; set; } = "C:/ProgramData/MawuGab/updates";
}
