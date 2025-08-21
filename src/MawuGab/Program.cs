using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MawuGab.Services;
using MawuGab.Core.Interfaces;
using MawuGab.Infrastructure.FileQueueing;
using MawuGab.Infrastructure.Security;
using MawuGab.Infrastructure.SystemAbstractions;
using MawuGab.Infrastructure.Logging;
using MawuGab;
using System.Diagnostics;
using System.Security.Principal;
#if WINDOWS
using Microsoft.Extensions.Hosting.WindowsServices;
#endif

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

#if WINDOWS
// When executed interactively, install and start the service automatically
var svcName = "M'awuGab Agent";
if (!WindowsServiceHelpers.IsWindowsService())
{
    if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
    {
        TryStopService(svcName);
        RunScCommand($"delete \"{svcName}\"");
        Console.WriteLine($"Service '{svcName}' uninstalled.");
        return;
    }

    EnsureElevated();
    var exePath = Process.GetCurrentProcess().MainModule!.FileName!;
    // Create service (if not exists), auto-start, with display name
    RunScCommand($"create \"{svcName}\" binPath= \"{exePath}\" start= auto DisplayName= \"{svcName}\"");
    // Set description (best-effort)
    RunScCommand($"description \"{svcName}\" \"MawuGab agent service to collect, compress and upload logs via SFTP.\"");
    // Start service
    RunScCommand($"start \"{svcName}\"");
    Console.WriteLine($"Service '{svcName}' installed and started. You can close this window.");
    return;
}
#endif

#if WINDOWS
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "M'awuGab Agent";
});
#endif

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddSimpleConsole();
});

// Options binding
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.Configure<SftpOptions>(builder.Configuration.GetSection("Sftp"));
builder.Services.Configure<UpdateOptions>(builder.Configuration.GetSection("Update"));

// DI registrations
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddSingleton<IAclManager, AclManager>();
builder.Services.AddSingleton<IFileQueue, FileQueue>();
builder.Services.AddSingleton<ICompressor, ZipCompressor>();
builder.Services.AddSingleton<ILogCollector, LogCollector>();
#if WINDOWS
builder.Services.AddSingleton<ISftpUploader, SftpUploader>();
#else
builder.Services.AddSingleton<ISftpUploader, NoopSftpUploader>();
#endif
builder.Services.AddSingleton<IUpdateManager, UpdateManager>();
builder.Services.AddSingleton<IMetricsServer, MetricsServer>();
builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Ensure required directories exist early
var cfg = host.Services.GetRequiredService<IConfiguration>();
var logsPath = cfg.GetSection("Agent").GetValue<string>("LogsPath") ?? "C:/ProgramData/MawuGab/logs";
var queuePath = cfg.GetSection("Agent").GetValue<string>("QueuePath") ?? "C:/ProgramData/MawuGab/queue";
var processedPath = cfg.GetSection("Agent").GetValue<string>("ProcessedPath") ?? "C:/ProgramData/MawuGab/processed";
Directory.CreateDirectory(logsPath);
Directory.CreateDirectory(queuePath);
Directory.CreateDirectory(processedPath);
host.Services.GetRequiredService<IAclManager>().EnsureDirectoryAccess(logsPath);
host.Services.GetRequiredService<IAclManager>().EnsureDirectoryAccess(queuePath);
host.Services.GetRequiredService<IAclManager>().EnsureDirectoryAccess(processedPath);

await host.RunAsync();

#if WINDOWS
static void EnsureElevated()
{
    using var identity = WindowsIdentity.GetCurrent();
    var principal = new WindowsPrincipal(identity);
    if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
    {
        Console.Error.WriteLine("Administrator privileges are required to install the service. Run this executable as Administrator.");
        Environment.Exit(1);
    }
}

static void RunScCommand(string arguments)
{
    var psi = new ProcessStartInfo
    {
        FileName = "sc.exe",
        Arguments = arguments,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    using var proc = Process.Start(psi)!;
    proc.WaitForExit();
}

static void TryStopService(string serviceName)
{
    try { RunScCommand($"stop \"{serviceName}\""); } catch { }
}
#endif
