namespace MawuGab.Core.Interfaces;

public interface ICompressor
{
    string CompressFile(string inputFilePath, string destinationDirectory);
}
