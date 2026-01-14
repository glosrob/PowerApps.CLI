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
            .Setup(x => x.GenerateEntityClass(It.IsAny<EntitySchema>(), It.IsAny<string>()))
            .Returns("class content");
        mockTemplateGenerator
            .Setup(x => x.GenerateSingleFile(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns("combined content");

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object);
        
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
            It.Is<string>(path => path.EndsWith("Entities.cs")),
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
            .Setup(x => x.GenerateGlobalOptionSetClass(It.IsAny<OptionSetSchema>(), It.IsAny<string>()))
            .Returns("optionset content");
        mockTemplateGenerator
            .Setup(x => x.GenerateSingleFile(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns("combined optionsets");

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object);
        
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
            It.Is<string>(path => path.EndsWith("OptionSets.cs")),
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
            .Setup(x => x.GenerateEntityClass(It.IsAny<EntitySchema>(), It.IsAny<string>()))
            .Returns("entity class content");

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object);
        
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
            .Setup(x => x.GenerateGlobalOptionSetClass(It.IsAny<OptionSetSchema>(), It.IsAny<string>()))
            .Returns("optionset class content");

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object);
        
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

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object);
        
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
            .Setup(x => x.GenerateEntityClass(It.IsAny<EntitySchema>(), It.IsAny<string>()))
            .Returns("content");
        mockTemplateGenerator
            .Setup(x => x.GenerateSingleFile(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns("combined");

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object);
        
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
        mockLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Entities.cs"))), Times.Once);
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
            .Setup(x => x.GenerateEntityClass(It.IsAny<EntitySchema>(), It.IsAny<string>()))
            .Returns("content");

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object);
        
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
            "MyCompany.Constants.Tables"), Times.Once);
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
            .Setup(x => x.GenerateGlobalOptionSetClass(It.IsAny<OptionSetSchema>(), It.IsAny<string>()))
            .Returns("content");

        var generator = new ConstantsGenerator(mockTemplateGenerator.Object, mockFilter.Object, mockFileWriter.Object);
        
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
            "MyCompany.Constants.Choices"), Times.Once);
    }
}
