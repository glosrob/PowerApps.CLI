using ClosedXML.Excel;
using Moq;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class SolutionLayerReporterTests : IDisposable
{
    private readonly Mock<IFileWriter> _mockFileWriter;
    private readonly SolutionLayerReporter _reporter;
    private readonly string _tempDirectory;

    public SolutionLayerReporterTests()
    {
        _mockFileWriter = new Mock<IFileWriter>();
        _reporter = new SolutionLayerReporter(_mockFileWriter.Object);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"PowerAppsCLI_SolutionLayer_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        _mockFileWriter
            .Setup(x => x.WriteBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Returns((string path, byte[] content) => File.WriteAllBytesAsync(path, content));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, true);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullFileWriter_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SolutionLayerReporter(null!));
    }

    #endregion

    #region File Output Tests

    [Fact]
    public async Task GenerateReportAsync_WritesFileToDisk()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");

        await _reporter.GenerateReportAsync(new SolutionLayerResult(), outputPath);

        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task GenerateReportAsync_CallsFileWriterWithCorrectPath()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");

        await _reporter.GenerateReportAsync(new SolutionLayerResult(), outputPath);

        _mockFileWriter.Verify(x => x.WriteBytesAsync(outputPath, It.IsAny<byte[]>()), Times.Once);
    }

    #endregion

    #region Sheet Structure Tests

    [Fact]
    public async Task GenerateReportAsync_NoUnmanagedLayers_OnlyCreatesSummarySheet()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var result = new SolutionLayerResult(); // HasUnmanagedLayers = false

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        Assert.Single(workbook.Worksheets);
        Assert.True(workbook.Worksheets.Contains("Summary"));
    }

    [Fact]
    public async Task GenerateReportAsync_WithUnmanagedLayers_CreatesBothSheets()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var result = new SolutionLayerResult
        {
            LayeredComponents = { MakeComponent("field1") }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        Assert.Equal(2, workbook.Worksheets.Count);
        Assert.True(workbook.Worksheets.Contains("Summary"));
        Assert.True(workbook.Worksheets.Contains("Unmanaged Layers"));
    }

    #endregion

    #region Summary Sheet Metadata Tests

    [Fact]
    public async Task GenerateReportAsync_WritesSolutionNameAndEnvironment()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var result = new SolutionLayerResult
        {
            SolutionName = "MySolution",
            EnvironmentUrl = "https://test.crm.dynamics.com"
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        Assert.Equal("MySolution", ws.Cell(3, 2).Value.ToString());
        Assert.Equal("https://test.crm.dynamics.com", ws.Cell(4, 2).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_WritesFormattedReportDate()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var result = new SolutionLayerResult
        {
            ReportDate = new DateTime(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc)
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        Assert.Equal("2026-06-13 12:00:00 UTC", ws.Cell(5, 2).Value.ToString());
    }

    #endregion

    #region Clean State Tests

    [Fact]
    public async Task GenerateReportAsync_NoUnmanagedLayers_ShowsCleanMessage()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");

        await _reporter.GenerateReportAsync(new SolutionLayerResult(), outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        Assert.Equal(
            "No unmanaged layers detected. All components are clean.",
            ws.Cell(7, 1).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_NoUnmanagedLayers_CleanMessageIsDarkGreen()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");

        await _reporter.GenerateReportAsync(new SolutionLayerResult(), outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        Assert.Equal(XLColor.DarkGreen, ws.Cell(7, 1).Style.Font.FontColor);
    }

    #endregion

    #region Unmanaged Layers — Summary Sheet Tests

    [Fact]
    public async Task GenerateReportAsync_WithUnmanagedLayers_ShowsWarningWithCount()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var result = new SolutionLayerResult
        {
            LayeredComponents = { MakeComponent("field1"), MakeComponent("field2") }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        Assert.Equal("WARNING: 2 component(s) have unmanaged layers.", ws.Cell(7, 1).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_WithUnmanagedLayers_WarningIsRed()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var result = new SolutionLayerResult
        {
            LayeredComponents = { MakeComponent("field1") }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        Assert.Equal(XLColor.Red, ws.Cell(7, 1).Style.Font.FontColor);
    }

    [Fact]
    public async Task GenerateReportAsync_SummarySheet_WritesComponentData()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var result = new SolutionLayerResult
        {
            LayeredComponents =
            {
                new LayeredComponent
                {
                    ComponentType = "Attribute",
                    ComponentName = "rob_name",
                    ParentEntity = "account",
                    AllLayers = new List<string> { "Active Solution", "Unmanaged" }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        // Row 7: warning, row 8: blank, row 9: header, row 10: data
        Assert.Equal("Attribute",                   ws.Cell(10, 1).Value.ToString());
        Assert.Equal("rob_name",                    ws.Cell(10, 2).Value.ToString());
        Assert.Equal("account",                     ws.Cell(10, 3).Value.ToString());
        Assert.Equal("Active Solution → Unmanaged", ws.Cell(10, 4).Value.ToString());
    }

    #endregion

    #region Unmanaged Layers Sheet Tests

    [Fact]
    public async Task GenerateReportAsync_LayersSheet_WritesComponentDetail()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var result = new SolutionLayerResult
        {
            LayeredComponents =
            {
                new LayeredComponent
                {
                    ComponentType = "Attribute",
                    ComponentName = "rob_status",
                    ParentEntity = "contact",
                    UnmanagedLayerOwner = "Rob",
                    AllLayers = new List<string> { "Base", "Rob" }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Unmanaged Layers");
        // Row 3 is header, data starts at row 4
        Assert.Equal("Attribute",  ws.Cell(4, 1).Value.ToString());
        Assert.Equal("rob_status", ws.Cell(4, 2).Value.ToString());
        Assert.Equal("contact",    ws.Cell(4, 3).Value.ToString());
        Assert.Equal("Rob",        ws.Cell(4, 4).Value.ToString());
        Assert.Equal("Base → Rob", ws.Cell(4, 5).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_LayersSheet_UnmanagedLayerOwnerCellIsRed()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var result = new SolutionLayerResult
        {
            LayeredComponents = { MakeComponent("rob_field") }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Unmanaged Layers");
        Assert.Equal(XLColor.Red, ws.Cell(4, 4).Style.Font.FontColor);
    }

    [Fact]
    public async Task GenerateReportAsync_LayersSheet_AllLayersJoinedWithArrow()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var result = new SolutionLayerResult
        {
            LayeredComponents =
            {
                new LayeredComponent
                {
                    ComponentName = "field1",
                    AllLayers = new List<string> { "Layer A", "Layer B", "Layer C" }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Unmanaged Layers");
        Assert.Equal("Layer A → Layer B → Layer C", ws.Cell(4, 5).Value.ToString());
    }

    #endregion

    #region Helper Methods

    private static LayeredComponent MakeComponent(string name) =>
        new()
        {
            ComponentName = name,
            ComponentType = "Attribute",
            ParentEntity = "account",
            UnmanagedLayerOwner = "Rob",
            AllLayers = new List<string> { "Managed", "Unmanaged" }
        };

    #endregion
}
