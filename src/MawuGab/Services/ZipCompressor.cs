using System.IO.Compression;
using MawuGab.Core.Interfaces;

namespace MawuGab.Services;

public sealed class ZipCompressor : ICompressor
{
    public string CompressFile(string inputFilePath, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        var output = Path.Combine(destinationDirectory, Path.GetFileName(inputFilePath) + ".zip");
        using var archive = ZipFile.Open(output, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(inputFilePath, Path.GetFileName(inputFilePath), CompressionLevel.Optimal);
        return output;
    }
}
