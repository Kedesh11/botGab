using MawuGab.Core.Models;

namespace MawuGab.Core.Interfaces;

public interface ILogCollector
{
    IEnumerable<LogFileInfo> DiscoverNewLogs();
}
