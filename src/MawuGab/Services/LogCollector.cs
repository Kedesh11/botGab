using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MawuGab.Core.Interfaces;
using MawuGab.Core.Models;

namespace MawuGab.Services;

public sealed class LogCollector : ILogCollector
{
    private static readonly Regex JrnPattern = new("^(\\d{8})\\.jrn$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly AgentOptions _options;
    private readonly ILogger<LogCollector> _logger;

    public LogCollector(IOptions<AgentOptions> options, ILogger<LogCollector> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public IEnumerable<LogFileInfo> DiscoverNewLogs()
    {
        var path = _options.LogSourcePath;
        if (!Directory.Exists(path)) yield break;

        foreach (var file in Directory.EnumerateFiles(path, "*.jrn", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (!JrnPattern.IsMatch(name)) continue;
            var (bank, gab) = ExtractBankAndGab(file);
            yield return new LogFileInfo(file, name, bank, gab);
        }
    }

    private (string bank, string gab) ExtractBankAndGab(string fullPath)
    {
        // Priority: configured values > path inference
        var bank = _options.BankName; var gab = _options.GabId;
        if (!string.IsNullOrWhiteSpace(bank) && !string.IsNullOrWhiteSpace(gab))
            return (bank!, gab!);

        // Try folder structure .../BANK/GAB/filename
        var dir = new DirectoryInfo(Path.GetDirectoryName(fullPath)!);
        var gabId = dir.Name;
        var bankName = dir.Parent?.Name ?? "UNKNOWN";
        bank = string.IsNullOrWhiteSpace(_options.BankName) ? bankName : _options.BankName;
        gab = string.IsNullOrWhiteSpace(_options.GabId) ? gabId : _options.GabId;
        return (bank!, gab!);
    }
}
