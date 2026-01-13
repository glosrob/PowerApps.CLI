namespace PowerApps.CLI.Infrastructure;

/// <summary>
/// Writes content to files on disk.
/// </summary>
public class FileWriter : IFileWriter
{
    public async Task WriteTextAsync(string filePath, string content)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        EnsureDirectoryExists(filePath);
        await File.WriteAllTextAsync(filePath, content);
    }

    public async Task WriteBytesAsync(string filePath, byte[] content)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        EnsureDirectoryExists(filePath);
        await File.WriteAllBytesAsync(filePath, content);
    }

    public bool FileExists(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        return File.Exists(filePath);
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
