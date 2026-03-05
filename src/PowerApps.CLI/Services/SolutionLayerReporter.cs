using ClosedXML.Excel;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Generates an Excel report from solution layer analysis results.
/// </summary>
public class SolutionLayerReporter : ISolutionLayerReporter
{
    private readonly IFileWriter _fileWriter;

    public SolutionLayerReporter(IFileWriter fileWriter)
    {
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public async Task GenerateReportAsync(SolutionLayerResult result, string outputPath)
    {
        using var workbook = new XLWorkbook();

        AddSummarySheet(workbook, result);

        if (result.HasUnmanagedLayers)
            AddLayersSheet(workbook, result);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        await _fileWriter.WriteBytesAsync(outputPath, stream.ToArray());
    }

    private static void AddSummarySheet(XLWorkbook workbook, SolutionLayerResult result)
    {
        var ws = workbook.Worksheets.Add("Summary");

        ws.Cell(1, 1).Value = "Solution Layer Report";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        var row = 3;
        ws.Cell(row, 1).Value = "Solution:";
        ws.Cell(row++, 2).Value = result.SolutionName;
        ws.Cell(row, 1).Value = "Environment:";
        ws.Cell(row++, 2).Value = result.EnvironmentUrl;
        ws.Cell(row, 1).Value = "Report Date:";
        ws.Cell(row++, 2).Value = result.ReportDate.ToString("yyyy-MM-dd HH:mm:ss UTC");

        row++; // blank row

        if (!result.HasUnmanagedLayers)
        {
            ws.Cell(row, 1).Value = "No unmanaged layers detected. All components are clean.";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.DarkGreen;
        }
        else
        {
            ws.Cell(row, 1).Value = $"WARNING: {result.LayeredComponents.Count} component(s) have unmanaged layers.";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.Red;
            row += 2;

            // Summary table header
            ws.Cell(row, 1).Value = "Component Type";
            ws.Cell(row, 2).Value = "Component Name";
            ws.Cell(row, 3).Value = "Table";
            ws.Cell(row, 4).Value = "Layer Stack (bottom to top)";

            var headerRange = ws.Range(row, 1, row, 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            row++;
            var startRow = row;

            foreach (var component in result.LayeredComponents)
            {
                ws.Cell(row, 1).Value = component.ComponentType;
                ws.Cell(row, 2).Value = component.ComponentName;
                ws.Cell(row, 3).Value = component.ParentEntity;
                ws.Cell(row, 4).Value = string.Join(" → ", component.AllLayers);
                ws.Cell(row, 4).Style.Font.FontColor = XLColor.Red;
                row++;
            }

            var tableRange = ws.Range(startRow - 1, 1, row - 1, 4);
            var table = tableRange.CreateTable("UnmanagedLayerSummary");
            table.Theme = XLTableTheme.TableStyleLight9;
        }

        ws.Columns().AdjustToContents();
    }

    private static void AddLayersSheet(XLWorkbook workbook, SolutionLayerResult result)
    {
        var ws = workbook.Worksheets.Add("Unmanaged Layers");

        ws.Cell(1, 1).Value = "Unmanaged Layer Detail";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 12;

        var row = 3;
        ws.Cell(row, 1).Value = "Component Type";
        ws.Cell(row, 2).Value = "Component Name";
        ws.Cell(row, 3).Value = "Table";
        ws.Cell(row, 4).Value = "Unmanaged Layer";
        ws.Cell(row, 5).Value = "Full Layer Stack (bottom to top)";

        var headerRange = ws.Range(row, 1, row, 5);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        row++;
        var startRow = row;

        foreach (var component in result.LayeredComponents)
        {
            ws.Cell(row, 1).Value = component.ComponentType;
            ws.Cell(row, 2).Value = component.ComponentName;
            ws.Cell(row, 3).Value = component.ParentEntity;
            ws.Cell(row, 4).Value = component.UnmanagedLayerOwner;
            ws.Cell(row, 4).Style.Font.FontColor = XLColor.Red;
            ws.Cell(row, 5).Value = string.Join(" → ", component.AllLayers);
            row++;
        }

        if (row > startRow)
        {
            var tableRange = ws.Range(startRow - 1, 1, row - 1, 5);
            var table = tableRange.CreateTable("LayerDetail");
            table.Theme = XLTableTheme.TableStyleLight9;
        }

        ws.Columns().AdjustToContents();
    }
}
