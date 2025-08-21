using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MawuGab.Services;
using System.IO;
using Xunit;

namespace MawuGab.Tests;

public class LogCollectorTests
{
    [Fact]
    public void Discover_Infer_Bank_And_Gab_From_Path_When_Not_Configured()
    {
        var root = Path.Combine(Path.GetTempPath(), "MawuGabTests", Guid.NewGuid().ToString("N"));
        var bankDir = Path.Combine(root, "BGFI");
        var gabDir = Path.Combine(bankDir, "ATM-001");
        Directory.CreateDirectory(gabDir);
        var file = Path.Combine(gabDir, "20250101.jrn");
        File.WriteAllText(file, "log");

        var options = Options.Create(new AgentOptions { LogSourcePath = root });
        var lc = new LogCollector(options, NullLogger<LogCollector>.Instance);

        var logs = lc.DiscoverNewLogs().ToList();
        logs.Should().HaveCount(1);
        logs[0].BankName.Should().Be("BGFI");
        logs[0].GabId.Should().Be("ATM-001");
    }
}
