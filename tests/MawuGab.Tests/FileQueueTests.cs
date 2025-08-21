using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MawuGab.Infrastructure.FileQueueing;
using MawuGab.Services;
using Xunit;

namespace MawuGab.Tests;

public class FileQueueTests
{
    [Fact]
    public void Enqueue_Persists_Zip_And_Metadata()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "MawuGabTests", Guid.NewGuid().ToString("N"));
        var queuePath = Path.Combine(root, "queue");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(queuePath);
        var opts = Options.Create(new AgentOptions { QueuePath = queuePath });
        var fq = new FileQueue(opts);

        var tempFile = Path.Combine(root, "20240101.jrn.zip");
        File.WriteAllText(tempFile, new string('x', 10));

        // Act
        var id = fq.Enqueue(tempFile, new Dictionary<string, string> { ["bank"] = "BGFI", ["gab"] = "ATM-001" });

        // Assert
        File.Exists(Path.Combine(queuePath, id + ".zip")).Should().BeTrue();
        var metaPath = Path.Combine(queuePath, id + ".meta");
        File.Exists(metaPath).Should().BeTrue();
        var meta = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(metaPath));
        meta.Should().NotBeNull();
        meta!["bank"].Should().Be("BGFI");
        meta["gab"].Should().Be("ATM-001");
    }

    [Fact]
    public void Peek_Orders_By_CreationTime()
    {
        var root = Path.Combine(Path.GetTempPath(), "MawuGabTests", Guid.NewGuid().ToString("N"));
        var queuePath = Path.Combine(root, "queue");
        Directory.CreateDirectory(queuePath);
        var fq = new FileQueue(Options.Create(new AgentOptions { QueuePath = queuePath }));

        var id1 = fq.Enqueue(CreateDummy(queuePath), null);
        System.Threading.Thread.Sleep(10);
        var id2 = fq.Enqueue(CreateDummy(queuePath), null);

        fq.Peek().Should().Be(id1);
        fq.MarkInProgress(id1);
        fq.Abandon(id1);
        fq.Peek().Should().Be(id1); // still first until completed
        fq.Complete(id1);
        fq.Peek().Should().Be(id2);
    }

    private static string CreateDummy(string folder)
    {
        var tmp = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".zip");
        File.WriteAllText(tmp, "dummy");
        return tmp;
    }
}
