using ClosedXML.Excel;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Generates Excel reports for process management results.
/// </summary>
public interface IProcessReporter
{
    Task GenerateReportAsync(ProcessManageSummary summary, string outputPath);
}

public class ProcessReporter : IProcessReporter
{
    private readonly IFileWriter _fileWriter;

    public ProcessReporter(IFileWriter fileWriter)
    {
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public async Task GenerateReportAsync(ProcessManageSummary summary, string outputPath)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Process Management");

        // Header
        worksheet.Cell(1, 1).Value = "Process Management Report";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        // Metadata
        var row = 3;
        worksheet.Cell(row++, 1).Value = "Environment:";
        worksheet.Cell(row - 1, 2).Value = summary.EnvironmentUrl;
        worksheet.Cell(row++, 1).Value = "Execution Date:";
        worksheet.Cell(row - 1, 2).Value = summary.ExecutionDate.ToString("yyyy-MM-dd HH:mm:ss UTC");
        worksheet.Cell(row++, 1).Value = "Mode:";
        worksheet.Cell(row - 1, 2).Value = summary.IsDryRun ? "Dry Run (Preview)" : "Executed";
        
        row++; // Blank row

        // Summary
        worksheet.Cell(row++, 1).Value = "Summary:";
        worksheet.Cell(row++, 1).Value = $"Total Processes: {summary.TotalProcesses}";
        worksheet.Cell(row++, 1).Value = $"Activated: {summary.ActivatedCount}";
        worksheet.Cell(row++, 1).Value = $"Deactivated: {summary.DeactivatedCount}";
        worksheet.Cell(row++, 1).Value = $"Unchanged: {summary.UnchangedCount}";
        worksheet.Cell(row++, 1).Value = $"Failed: {summary.FailedCount}";
        
        if (summary.HasFailures)
        {
            worksheet.Cell(row - 1, 1).Style.Font.FontColor = XLColor.Red;
        }

        row++; // Blank row

        // Table header
        worksheet.Cell(row, 1).Value = "Process Name";
        worksheet.Cell(row, 2).Value = "Type";
        worksheet.Cell(row, 3).Value = "Expected State";
        worksheet.Cell(row, 4).Value = "Actual State";
        worksheet.Cell(row, 5).Value = "Action Taken";
        worksheet.Cell(row, 6).Value = "Error Message";

        var headerRow = worksheet.Range(row, 1, row, 6);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        row++;

        // Process data - sorted by process name
        foreach (var result in summary.Results.OrderBy(r => r.Process.Name))
        {
            worksheet.Cell(row, 1).Value = result.Process.Name;
            worksheet.Cell(row, 2).Value = GetProcessTypeName(result.Process.Type);
            worksheet.Cell(row, 3).Value = result.Process.ExpectedState.ToString();
            worksheet.Cell(row, 4).Value = result.Process.CurrentState.ToString();
            worksheet.Cell(row, 5).Value = GetActionText(result.Action);
            worksheet.Cell(row, 6).Value = result.ErrorMessage ?? string.Empty;

            // Color code the action
            if (result.Action == ProcessAction.Failed)
            {
                worksheet.Cell(row, 5).Style.Font.FontColor = XLColor.Red;
            }
            else if (result.Action == ProcessAction.Activated || result.Action == ProcessAction.Deactivated)
            {
                worksheet.Cell(row, 5).Style.Font.FontColor = XLColor.Blue;
            }

            row++;
        }

        // Create Excel table
        if (summary.Results.Any())
        {
            var tableRange = worksheet.Range(row - summary.Results.Count - 1, 1, row - 1, 6);
            var excelTable = tableRange.CreateTable();
            excelTable.Theme = XLTableTheme.TableStyleLight9;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Save workbook
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        await _fileWriter.WriteBytesAsync(outputPath, stream.ToArray());
    }

    private string GetProcessTypeName(ProcessType type)
    {
        return type switch
        {
            ProcessType.Workflow => "Workflow",
            ProcessType.BusinessRule => "Business Rule",
            ProcessType.Action => "Action",
            ProcessType.CloudFlow => "Cloud Flow",
            _ => "Unknown"
        };
    }

    private string GetActionText(ProcessAction action)
    {
        return action switch
        {
            ProcessAction.NoChangeNeeded => "Unchanged",
            ProcessAction.Activated => "Activated",
            ProcessAction.Deactivated => "Deactivated",
            ProcessAction.Failed => "Failed",
            _ => "Unknown"
        };
    }
}
