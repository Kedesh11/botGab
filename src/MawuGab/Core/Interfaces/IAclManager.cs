namespace MawuGab.Core.Interfaces;

public interface IAclManager
{
    void EnsureDirectoryAccess(string path);
}
