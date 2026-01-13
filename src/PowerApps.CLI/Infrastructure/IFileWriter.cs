namespace PowerApps.CLI.Infrastructure;

/// <summary>
/// Provides file writing functionality.
/// </summary>
public interface IFileWriter
{
    /// <summary>
    /// Writes text content to a file asynchronously.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    /// <param name="filePath">The path to the file to write.</param>
    /// <param name="content">The text content to write.</param>
    Task WriteTextAsync(string filePath, string content);

    /// <summary>
    /// Writes binary content to a file asynchronously.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    /// <param name="filePath">The path to the file to write.</param>
    /// <param name="content">The binary content to write.</param>
    Task WriteBytesAsync(string filePath, byte[] content);

    /// <summary>
    /// Checks if a file exists.
    /// </summary>
    /// <param name="filePath">The path to the file to check.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    bool FileExists(string filePath);
}
