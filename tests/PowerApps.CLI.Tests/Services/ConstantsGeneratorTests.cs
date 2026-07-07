using Moq;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class ConstantsGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_SingleFileMode_GeneratesEntitiesFileAsync()
    {
        // Arrange
        var mockTemplateGenerator = new Mock<ICodeTemplateGenerator>();
        var mockFilter = new Mock<IConstantsFilter>();
        var mockFileWriter = new Mock<IFileWriter>();
        var mockLogger = new Mock<IConsoleLogger>();

        mockTemplateGenerator
            .Setup(x => x.GenerateEntityClass(It.IsAny<EntitySchema>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("class content");
        mockTemplateGenerator
            .Setup(x => x.GenerateSingleFile(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns("combined content");

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object, new IdentifierFormatter());

        var entities = new List<EntitySchema>
        {
            new EntitySchema { LogicalName = "contact" }
        };
        var outputConfig = new ConstantsOutputConfig
        {
            OutputPath = "./output",
            Namespace = "MyCompany.Constants",
            SingleFile = true,
            IncludeEntities = true,
            IncludeGlobalOptionSets = false
        };

        // Act
        await generator.GenerateAsync(entities, outputConfig, mockLogger.Object);

        // Assert
        mockFileWriter.Verify(x => x.WriteTextAsync(
            It.Is<string>(path => path.EndsWith("Tables.cs")),
            "combined content"), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_SingleFileMode_GeneratesOptionSetsFileAsync()
    {
        // Arrange
        var mockTemplateGenerator = new Mock<ICodeTemplateGenerator>();
        var mockFilter = new Mock<IConstantsFilter>();
        var mockFileWriter = new Mock<IFileWriter>();
        var mockLogger = new Mock<IConsoleLogger>();

        var globalOptionSets = new List<OptionSetSchema>
        {
            new OptionSetSchema { Name = "statuscode", IsGlobal = true }
        };

        mockFilter
            .Setup(x => x.ExtractGlobalOptionSets(It.IsAny<List<EntitySchema>>()))
            .Returns(globalOptionSets);
        mockTemplateGenerator
            .Setup(x => x.GenerateGlobalOptionSetClass(It.IsAny<OptionSetSchema>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("optionset content");
        mockTemplateGenerator
            .Setup(x => x.GenerateSingleFile(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns("combined optionsets");

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object, new IdentifierFormatter());

        var entities = new List<EntitySchema> { new EntitySchema { LogicalName = "contact" } };
        var outputConfig = new ConstantsOutputConfig
        {
            OutputPath = "./output",
            Namespace = "MyCompany.Constants",
            SingleFile = true,
            IncludeEntities = false,
            IncludeGlobalOptionSets = true
        };

        // Act
        await generator.GenerateAsync(entities, outputConfig, mockLogger.Object);

        // Assert
        mockFileWriter.Verify(x => x.WriteTextAsync(
            It.Is<string>(path => path.EndsWith("Choices.cs")),
            "combined optionsets"), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_MultipleFilesMode_GeneratesEntityFilesAsync()
    {
        // Arrange
        var mockTemplateGenerator = new Mock<ICodeTemplateGenerator>();
        var mockFilter = new Mock<IConstantsFilter>();
        var mockFileWriter = new Mock<IFileWriter>();
        var mockLogger = new Mock<IConsoleLogger>();

        mockTemplateGenerator
            .Setup(x => x.GenerateEntityClass(It.IsAny<EntitySchema>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("entity class content");

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object, new IdentifierFormatter());

        var entities = new List<EntitySchema>
        {
            new EntitySchema { LogicalName = "contact", DisplayName = "Contact" },
            new EntitySchema { LogicalName = "account", DisplayName = "Account" }
        };
        var outputConfig = new ConstantsOutputConfig
        {
            OutputPath = "./output",
            Namespace = "MyCompany.Constants",
            SingleFile = false,
            IncludeEntities = true,
            IncludeGlobalOptionSets = false
        };

        // Act
        await generator.GenerateAsync(entities, outputConfig, mockLogger.Object);

        // Assert
        mockFileWriter.Verify(x => x.WriteTextAsync(
            It.Is<string>(path => path.Contains("Tables") && path.EndsWith("Contact.cs")),
            "entity class content"), Times.Once);
        mockFileWriter.Verify(x => x.WriteTextAsync(
            It.Is<string>(path => path.Contains("Tables") && path.EndsWith("Account.cs")),
            "entity class content"), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_MultipleFilesMode_GeneratesOptionSetFilesAsync()
    {
        // Arrange
        var mockTemplateGenerator = new Mock<ICodeTemplateGenerator>();
        var mockFilter = new Mock<IConstantsFilter>();
        var mockFileWriter = new Mock<IFileWriter>();
        var mockLogger = new Mock<IConsoleLogger>();

        var globalOptionSets = new List<OptionSetSchema>
        {
            new OptionSetSchema { Name = "statuscode", IsGlobal = true },
            new OptionSetSchema { Name = "statecode", IsGlobal = true }
        };

        mockFilter
            .Setup(x => x.ExtractGlobalOptionSets(It.IsAny<List<EntitySchema>>()))
            .Returns(globalOptionSets);
        mockTemplateGenerator
            .Setup(x => x.GenerateGlobalOptionSetClass(It.IsAny<OptionSetSchema>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("optionset class content");

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object, new IdentifierFormatter());

        var entities = new List<EntitySchema> { new EntitySchema { LogicalName = "contact" } };
        var outputConfig = new ConstantsOutputConfig
        {
            OutputPath = "./output",
            Namespace = "MyCompany.Constants",
            SingleFile = false,
            IncludeEntities = false,
            IncludeGlobalOptionSets = true
        };

        // Act
        await generator.GenerateAsync(entities, outputConfig, mockLogger.Object);

        // Assert
        mockFileWriter.Verify(x => x.WriteTextAsync(
            It.Is<string>(path => path.Contains("Choices") && path.EndsWith("Statuscode.cs")),
            "optionset class content"), Times.Once);
        mockFileWriter.Verify(x => x.WriteTextAsync(
            It.Is<string>(path => path.Contains("Choices") && path.EndsWith("Statecode.cs")),
            "optionset class content"), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_EntitiesDisabled_DoesNotGenerateEntityFilesAsync()
    {
        // Arrange
        var mockTemplateGenerator = new Mock<ICodeTemplateGenerator>();
        var mockFilter = new Mock<IConstantsFilter>();
        var mockFileWriter = new Mock<IFileWriter>();
        var mockLogger = new Mock<IConsoleLogger>();

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object, new IdentifierFormatter());

        var entities = new List<EntitySchema>
        {
            new EntitySchema { LogicalName = "contact" }
        };
        var outputConfig = new ConstantsOutputConfig
        {
            OutputPath = "./output",
            Namespace = "MyCompany.Constants",
            SingleFile = true,
            IncludeEntities = false,
            IncludeGlobalOptionSets = false
        };

        // Act
        await generator.GenerateAsync(entities, outputConfig, mockLogger.Object);

        // Assert
        mockFileWriter.Verify(x => x.WriteTextAsync(
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_LogsProgressAsync()
    {
        // Arrange
        var mockTemplateGenerator = new Mock<ICodeTemplateGenerator>();
        var mockFilter = new Mock<IConstantsFilter>();
        var mockFileWriter = new Mock<IFileWriter>();
        var mockLogger = new Mock<IConsoleLogger>();

        mockTemplateGenerator
            .Setup(x => x.GenerateEntityClass(It.IsAny<EntitySchema>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("content");
        mockTemplateGenerator
            .Setup(x => x.GenerateSingleFile(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns("combined");

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object, new IdentifierFormatter());

        var entities = new List<EntitySchema>
        {
            new EntitySchema { LogicalName = "contact" },
            new EntitySchema { LogicalName = "account" }
        };
        var outputConfig = new ConstantsOutputConfig
        {
            OutputPath = "./output",
            Namespace = "MyCompany.Constants",
            SingleFile = true,
            IncludeEntities = true,
            IncludeGlobalOptionSets = false
        };

        // Act
        await generator.GenerateAsync(entities, outputConfig, mockLogger.Object);

        // Assert
        mockLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("./output"))), Times.AtLeastOnce);
        mockLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Tables.cs"))), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_UsesCorrectNamespaceForMultipleFilesAsync()
    {
        // Arrange
        var mockTemplateGenerator = new Mock<ICodeTemplateGenerator>();
        var mockFilter = new Mock<IConstantsFilter>();
        var mockFileWriter = new Mock<IFileWriter>();
        var mockLogger = new Mock<IConsoleLogger>();

        mockTemplateGenerator
            .Setup(x => x.GenerateEntityClass(It.IsAny<EntitySchema>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("content");

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object, new IdentifierFormatter());

        var entities = new List<EntitySchema>
        {
            new EntitySchema { LogicalName = "contact", DisplayName = "Contact" }
        };
        var outputConfig = new ConstantsOutputConfig
        {
            OutputPath = "./output",
            Namespace = "MyCompany.Constants",
            SingleFile = false,
            IncludeEntities = true,
            IncludeGlobalOptionSets = false
        };

        // Act
        await generator.GenerateAsync(entities, outputConfig, mockLogger.Object);

        // Assert
        mockTemplateGenerator.Verify(x => x.GenerateEntityClass(
            It.IsAny<EntitySchema>(),
            "MyCompany.Constants.Tables",
            "Contact"), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_MultipleFiles_UsesCorrectNamespaceForOptionSetsAsync()
    {
        // Arrange
        var mockTemplateGenerator = new Mock<ICodeTemplateGenerator>();
        var mockFilter = new Mock<IConstantsFilter>();
        var mockFileWriter = new Mock<IFileWriter>();
        var mockLogger = new Mock<IConsoleLogger>();

        var globalOptionSets = new List<OptionSetSchema>
        {
            new OptionSetSchema { Name = "statuscode", IsGlobal = true }
        };

        mockFilter
            .Setup(x => x.ExtractGlobalOptionSets(It.IsAny<List<EntitySchema>>()))
            .Returns(globalOptionSets);
        mockTemplateGenerator
            .Setup(x => x.GenerateGlobalOptionSetClass(It.IsAny<OptionSetSchema>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("content");

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object, new IdentifierFormatter());

        var entities = new List<EntitySchema> { new EntitySchema { LogicalName = "contact" } };
        var outputConfig = new ConstantsOutputConfig
        {
            OutputPath = "./output",
            Namespace = "MyCompany.Constants",
            SingleFile = false,
            IncludeEntities = false,
            IncludeGlobalOptionSets = true
        };

        // Act
        await generator.GenerateAsync(entities, outputConfig, mockLogger.Object);

        // Assert
        mockTemplateGenerator.Verify(x => x.GenerateGlobalOptionSetClass(
            It.IsAny<OptionSetSchema>(),
            "MyCompany.Constants.Choices",
            "Statuscode"), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_SingleFileMode_TwoTablesWithSameDisplayName_DeduplicatesClassNamesAsync()
    {
        // Arrange
        var mockFilter = new Mock<IConstantsFilter>();
        var mockFileWriter = new Mock<IFileWriter>();
        var mockLogger = new Mock<IConsoleLogger>();
        var templateGenerator = new CodeTemplateGenerator(true, true, new IdentifierFormatter());

        string? capturedContent = null;
        mockFileWriter
            .Setup(x => x.WriteTextAsync(It.Is<string>(p => p.EndsWith("Tables.cs")), It.IsAny<string>()))
            .Callback<string, string>((_, content) => capturedContent = content)
            .Returns(Task.CompletedTask);

        var generator = new ConstantsGenerator(templateGenerator, mockFilter.Object, mockFileWriter.Object, new IdentifierFormatter());

        var entities = new List<EntitySchema>
        {
            new EntitySchema { LogicalName = "rob_email", DisplayName = "Email" },
            new EntitySchema { LogicalName = "email", DisplayName = "Email" }
        };
        var outputConfig = new ConstantsOutputConfig
        {
            OutputPath = "./output",
            Namespace = "MyCompany.Constants",
            SingleFile = true,
            IncludeEntities = true,
            IncludeGlobalOptionSets = false
        };

        // Act
        await generator.GenerateAsync(entities, outputConfig, mockLogger.Object);

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains("public static class Email", capturedContent);
        Assert.Contains("public static class Email_", capturedContent);
    }

    [Fact]
    public async Task GenerateAsync_SingleFileMode_TwoGlobalOptionSetsWithSameDisplayName_DeduplicatesClassNamesAsync()
    {
        // Arrange
        var mockFilter = new Mock<IConstantsFilter>();
        var mockFileWriter = new Mock<IFileWriter>();
        var mockLogger = new Mock<IConsoleLogger>();
        var templateGenerator = new CodeTemplateGenerator(true, true, new IdentifierFormatter());

        var globalOptionSets = new List<OptionSetSchema>
        {
            new OptionSetSchema { Name = "rob_priority", DisplayName = "Priority", IsGlobal = true },
            new OptionSetSchema { Name = "xrt_priority", DisplayName = "Priority", IsGlobal = true }
        };
        mockFilter
            .Setup(x => x.ExtractGlobalOptionSets(It.IsAny<List<EntitySchema>>()))
            .Returns(globalOptionSets);

        string? capturedContent = null;
        mockFileWriter
            .Setup(x => x.WriteTextAsync(It.Is<string>(p => p.EndsWith("Choices.cs")), It.IsAny<string>()))
            .Callback<string, string>((_, content) => capturedContent = content)
            .Returns(Task.CompletedTask);

        var generator = new ConstantsGenerator(templateGenerator, mockFilter.Object, mockFileWriter.Object, new IdentifierFormatter());

        var entities = new List<EntitySchema> { new EntitySchema { LogicalName = "contact" } };
        var outputConfig = new ConstantsOutputConfig
        {
            OutputPath = "./output",
            Namespace = "MyCompany.Constants",
            SingleFile = true,
            IncludeEntities = false,
            IncludeGlobalOptionSets = true
        };

        // Act
        await generator.GenerateAsync(entities, outputConfig, mockLogger.Object);

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains("public static class Priority", capturedContent);
        Assert.Contains("public static class Priority_", capturedContent);
    }

    [Fact]
    public async Task GenerateAsync_MultipleFilesMode_TwoTablesWithSameDisplayName_DeduplicatesFileAndClassNamesAsync()
    {
        // Arrange
        var mockFilter = new Mock<IConstantsFilter>();
        var mockFileWriter = new Mock<IFileWriter>();
        var mockLogger = new Mock<IConsoleLogger>();
        var templateGenerator = new CodeTemplateGenerator(true, true, new IdentifierFormatter());

        var writtenFiles = new List<(string Path, string Content)>();
        mockFileWriter
            .Setup(x => x.WriteTextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((path, content) => writtenFiles.Add((path, content)))
            .Returns(Task.CompletedTask);

        var generator = new ConstantsGenerator(templateGenerator, mockFilter.Object, mockFileWriter.Object, new IdentifierFormatter());

        var entities = new List<EntitySchema>
        {
            new EntitySchema { LogicalName = "rob_email", DisplayName = "Email" },
            new EntitySchema { LogicalName = "email", DisplayName = "Email" }
        };
        var outputConfig = new ConstantsOutputConfig
        {
            OutputPath = "./output",
            Namespace = "MyCompany.Constants",
            SingleFile = false,
            IncludeEntities = true,
            IncludeGlobalOptionSets = false
        };

        // Act
        await generator.GenerateAsync(entities, outputConfig, mockLogger.Object);

        // Assert — two distinct files, not a silent overwrite, each with a distinct class name
        Assert.Equal(2, writtenFiles.Count);
        Assert.NotEqual(writtenFiles[0].Path, writtenFiles[1].Path);
        Assert.Contains("public static class Email", writtenFiles[0].Content);
        Assert.Contains("public static class Email_", writtenFiles[1].Content);
    }
}
