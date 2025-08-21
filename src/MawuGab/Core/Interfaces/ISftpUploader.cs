namespace MawuGab.Core.Interfaces;

public interface ISftpUploader
{
    Task<bool> UploadAsync(string localFilePath, string remotePath, CancellationToken ct);
}
