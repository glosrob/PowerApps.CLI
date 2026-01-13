namespace PowerApps.CLI.Tests.Infrastructure;

using PowerApps.CLI.Infrastructure;

public class FileWriterTests : IDisposable
{
    private readonly FileWriter _fileWriter;
    private readonly string _testDirectory;

    public FileWriterTests()
    {
        _fileWriter = new FileWriter();
        _testDirectory = Path.Combine(Path.GetTempPath(), "PowerApps.CLI.Tests", Guid.NewGuid().ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task WriteTextAsync_CreatesFileWithContentAsync()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        const string content = "Hello, World!";

        // Act
        await _fileWriter.WriteTextAsync(filePath, content);

        // Assert
        Assert.True(File.Exists(filePath));
        var writtenContent = await File.ReadAllTextAsync(filePath);
        Assert.Equal(content, writtenContent);
    }

    [Fact]
    public async Task WriteTextAsync_CreatesDirectoryIfNotExistsAsync()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "subfolder", "nested", "test.txt");
        const string content = "Test content";

        // Act
        await _fileWriter.WriteTextAsync(filePath, content);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(Directory.Exists(Path.GetDirectoryName(filePath)));
    }

    [Fact]
    public async Task WriteTextAsync_OverwritesExistingFileAsync()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        const string originalContent = "Original content";
        const string newContent = "New content";

        // Act
        await _fileWriter.WriteTextAsync(filePath, originalContent);
        await _fileWriter.WriteTextAsync(filePath, newContent);

        // Assert
        var writtenContent = await File.ReadAllTextAsync(filePath);
        Assert.Equal(newContent, writtenContent);
    }

    [Fact]
    public async Task WriteTextAsync_WithEmptyString_WritesEmptyFileAsync()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "empty.txt");

        // Act
        await _fileWriter.WriteTextAsync(filePath, string.Empty);

        // Assert
        Assert.True(File.Exists(filePath));
        var writtenContent = await File.ReadAllTextAsync(filePath);
        Assert.Empty(writtenContent);
    }

    [Fact]
    public async Task WriteTextAsync_WithNullFilePath_ThrowsArgumentExceptionAsync()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _fileWriter.WriteTextAsync(null!, "content"));
    }

    [Fact]
    public async Task WriteTextAsync_WithWhitespaceFilePath_ThrowsArgumentExceptionAsync()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _fileWriter.WriteTextAsync("   ", "content"));
    }

    [Fact]
    public async Task WriteTextAsync_WithNullContent_ThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _fileWriter.WriteTextAsync(filePath, null!));
    }

    [Fact]
    public async Task WriteBytesAsync_CreatesFileWithContentAsync()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.bin");
        var content = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Act
        await _fileWriter.WriteBytesAsync(filePath, content);

        // Assert
        Assert.True(File.Exists(filePath));
        var writtenContent = await File.ReadAllBytesAsync(filePath);
        Assert.Equal(content, writtenContent);
    }

    [Fact]
    public async Task WriteBytesAsync_CreatesDirectoryIfNotExistsAsync()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "subfolder", "test.bin");
        var content = new byte[] { 0xFF, 0xFE };

        // Act
        await _fileWriter.WriteBytesAsync(filePath, content);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(Directory.Exists(Path.GetDirectoryName(filePath)));
    }

    [Fact]
    public async Task WriteBytesAsync_WithEmptyArray_WritesEmptyFileAsync()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "empty.bin");
        var content = Array.Empty<byte>();

        // Act
        await _fileWriter.WriteBytesAsync(filePath, content);

        // Assert
        Assert.True(File.Exists(filePath));
        var writtenContent = await File.ReadAllBytesAsync(filePath);
        Assert.Empty(writtenContent);
    }

    [Fact]
    public async Task WriteBytesAsync_WithNullFilePath_ThrowsArgumentExceptionAsync()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _fileWriter.WriteBytesAsync(null!, new byte[] { 0x01 }));
    }

    [Fact]
    public async Task WriteBytesAsync_WithNullContent_ThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.bin");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _fileWriter.WriteBytesAsync(filePath, null!));
    }

    [Fact]
    public void FileExists_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "exists.txt");
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(filePath, "content");

        // Act
        var result = _fileWriter.FileExists(filePath);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void FileExists_WithNonExistingFile_ReturnsFalse()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "does-not-exist.txt");

        // Act
        var result = _fileWriter.FileExists(filePath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void FileExists_WithNullPath_ReturnsFalse()
    {
        // Act
        var result = _fileWriter.FileExists(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void FileExists_WithEmptyPath_ReturnsFalse()
    {
        // Act
        var result = _fileWriter.FileExists(string.Empty);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task WriteTextAsync_WithMultilineContent_PreservesLineBreaksAsync()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "multiline.txt");
        const string content = "Line 1\nLine 2\r\nLine 3";

        // Act
        await _fileWriter.WriteTextAsync(filePath, content);

        // Assert
        var writtenContent = await File.ReadAllTextAsync(filePath);
        Assert.Equal(content, writtenContent);
    }

    [Fact]
    public async Task WriteTextAsync_WithSpecialCharacters_PreservesContentAsync()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "special.txt");
        const string content = "Special: !@#$%^&*(){}[]|\\:;<>?,./~`";

        // Act
        await _fileWriter.WriteTextAsync(filePath, content);

        // Assert
        var writtenContent = await File.ReadAllTextAsync(filePath);
        Assert.Equal(content, writtenContent);
    }

    [Fact]
    public async Task WriteTextAsync_WithUnicodeContent_PreservesContentAsync()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "unicode.txt");
        const string content = "Hello 世界 🌍 Привет";

        // Act
        await _fileWriter.WriteTextAsync(filePath, content);

        // Assert
        var writtenContent = await File.ReadAllTextAsync(filePath);
        Assert.Equal(content, writtenContent);
    }
}
