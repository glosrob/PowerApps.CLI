using ClosedXML.Excel;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

public class DataPatchReporter : IDataPatchReporter
{
    private readonly IFileWriter _fileWriter;

    public DataPatchReporter(IFileWriter fileWriter)
    {
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public async Task GenerateReportAsync(DataPatchSummary summary, string outputPath)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Data Patch");

        // Header
        worksheet.Cell(1, 1).Value = "Data Patch Report";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        // Metadata
        var row = 3;
        worksheet.Cell(row, 1).Value = "Environment:";
        worksheet.Cell(row++, 2).Value = summary.EnvironmentUrl;
        worksheet.Cell(row, 1).Value = "Execution Date:";
        worksheet.Cell(row++, 2).Value = summary.ExecutionDate.ToString("yyyy-MM-dd HH:mm:ss UTC");

        row++; // Blank row

        // Summary
        worksheet.Cell(row++, 1).Value = "Summary:";
        worksheet.Cell(row++, 1).Value = $"Updated:   {summary.UpdatedCount}";
        worksheet.Cell(row++, 1).Value = $"Unchanged: {summary.UnchangedCount}";

        var failedCell = worksheet.Cell(row++, 1);
        failedCell.Value = $"Failed:    {summary.FailedCount}";
        if (summary.HasFailures)
            failedCell.Style.Font.FontColor = XLColor.Red;

        row++; // Blank row

        // Table header
        worksheet.Cell(row, 1).Value = "Entity";
        worksheet.Cell(row, 2).Value = "Key";
        worksheet.Cell(row, 3).Value = "Field";
        worksheet.Cell(row, 4).Value = "Old Value";
        worksheet.Cell(row, 5).Value = "New Value";
        worksheet.Cell(row, 6).Value = "Status";

        var headerRange = worksheet.Range(row, 1, row, 6);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        var tableStartRow = row;
        row++;

        // Results
        foreach (var result in summary.Results)
        {
            worksheet.Cell(row, 1).Value = result.Entity;
            worksheet.Cell(row, 2).Value = result.Key;
            worksheet.Cell(row, 3).Value = result.Field;
            worksheet.Cell(row, 4).Value = result.OldValue ?? string.Empty;
            worksheet.Cell(row, 5).Value = result.NewValue ?? string.Empty;

            var statusText = result.Status switch
            {
                PatchStatus.Updated        => "Updated",
                PatchStatus.Unchanged      => "Unchanged",
                PatchStatus.NotFound       => $"Not Found",
                PatchStatus.AmbiguousMatch => "Ambiguous Match",
                PatchStatus.Error          => $"Error: {result.ErrorMessage}",
                _                          => result.Status.ToString()
            };

            var statusCell = worksheet.Cell(row, 6);
            statusCell.Value = statusText;
            statusCell.Style.Font.FontColor = result.Status switch
            {
                PatchStatus.Updated        => XLColor.DarkGreen,
                PatchStatus.Unchanged      => XLColor.Gray,
                PatchStatus.NotFound       => XLColor.Red,
                PatchStatus.AmbiguousMatch => XLColor.Red,
                PatchStatus.Error          => XLColor.Red,
                _                          => XLColor.Black
            };

            row++;
        }

        // Create Excel table
        if (summary.Results.Any())
        {
            var tableRange = worksheet.Range(tableStartRow, 1, row - 1, 6);
            var excelTable = tableRange.CreateTable();
            excelTable.Theme = XLTableTheme.TableStyleLight9;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        await _fileWriter.WriteBytesAsync(outputPath, stream.ToArray());
    }
}
