using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;
using MawuGab.Services;

namespace MawuGab.Infrastructure.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logFile;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly object _writeLock = new();

    public FileLoggerProvider(IOptions<AgentOptions> options)
    {
        var logsDir = options.Value.LogsPath;
        Directory.CreateDirectory(logsDir);
        _logFile = Path.Combine(logsDir, "agent.log");
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(_logFile, _writeLock, name));
    }

    public void Dispose() { }

    private sealed class FileLogger : ILogger
    {
        private readonly string _filePath;
        private readonly object _lock;
        private readonly string _name;

        public FileLogger(string filePath, object writeLock, string name)
        {
            _filePath = filePath;
            _lock = writeLock;
            _name = name;
        }

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var line = $"{DateTime.UtcNow:O} [{logLevel}] {_name} - {formatter(state, exception)}";
            if (exception != null) line += " | " + exception;
            lock (_lock)
            {
                File.AppendAllText(_filePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
