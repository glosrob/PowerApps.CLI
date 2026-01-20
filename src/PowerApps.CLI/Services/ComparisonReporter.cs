using ClosedXML.Excel;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Generates Excel reports from comparison results.
/// </summary>
public class ComparisonReporter : IComparisonReporter
{
    private readonly IFileWriter _fileWriter;

    public ComparisonReporter(IFileWriter fileWriter)
    {
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public async Task GenerateReportAsync(ComparisonResult result, string outputPath)
    {
        using var workbook = new XLWorkbook();

        // Add summary sheet
        AddSummarySheet(workbook, result);

        // Add detail sheets only for tables with differences
        foreach (var tableResult in result.TableResults.Where(t => t.HasDifferences))
        {
            AddTableDetailSheet(workbook, tableResult);
        }

        // Save workbook
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        await _fileWriter.WriteBytesAsync(outputPath, stream.ToArray());
    }

    private void AddSummarySheet(XLWorkbook workbook, ComparisonResult result)
    {
        var worksheet = workbook.Worksheets.Add("Summary");

        // Header
        worksheet.Cell(1, 1).Value = "Reference Data Comparison Report";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        // Metadata
        var row = 3;
        worksheet.Cell(row++, 1).Value = "Source Environment:";
        worksheet.Cell(row - 1, 2).Value = result.SourceEnvironment;
        worksheet.Cell(row++, 1).Value = "Target Environment:";
        worksheet.Cell(row - 1, 2).Value = result.TargetEnvironment;
        worksheet.Cell(row++, 1).Value = "Comparison Date:";
        worksheet.Cell(row - 1, 2).Value = result.ComparisonDate.ToString("yyyy-MM-dd HH:mm:ss UTC");
        
        row++; // Blank row

        if (!result.HasAnyDifferences)
        {
            worksheet.Cell(row, 1).Value = "No differences found - all tables are in sync.";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontColor = XLColor.Green;
        }
        else
        {
            // Table summary header
            worksheet.Cell(row, 1).Value = "Table";
            worksheet.Cell(row, 2).Value = "Source Count";
            worksheet.Cell(row, 3).Value = "Target Count";
            worksheet.Cell(row, 4).Value = "New";
            worksheet.Cell(row, 5).Value = "Modified";
            worksheet.Cell(row, 6).Value = "Deleted";
            worksheet.Cell(row, 7).Value = "Status";

            var headerRow = worksheet.Range(row, 1, row, 7);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;
            headerRow.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            row++;

            // Table data
            foreach (var tableResult in result.TableResults.OrderBy(t => t.TableName))
            {
                worksheet.Cell(row, 1).Value = tableResult.TableName;
                worksheet.Cell(row, 2).Value = tableResult.SourceRecordCount;
                worksheet.Cell(row, 3).Value = tableResult.TargetRecordCount;
                worksheet.Cell(row, 4).Value = tableResult.NewCount;
                worksheet.Cell(row, 5).Value = tableResult.ModifiedCount;
                worksheet.Cell(row, 6).Value = tableResult.DeletedCount;
                
                var status = tableResult.HasDifferences ? "Differences Found" : "In Sync";
                worksheet.Cell(row, 7).Value = status;
                
                if (tableResult.HasDifferences)
                {
                    worksheet.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
                    
                    // Create hyperlink to detail sheet
                    var detailSheetName = SanitizeSheetName(tableResult.TableName);
                    worksheet.Cell(row, 1).SetHyperlink(new XLHyperlink($"'{detailSheetName}'!A1"));
                    worksheet.Cell(row, 1).Style.Font.FontColor = XLColor.Blue;
                    worksheet.Cell(row, 1).Style.Font.Underline = XLFontUnderlineValues.Single;
                }
                else
                {
                    worksheet.Cell(row, 7).Style.Font.FontColor = XLColor.Green;
                }

                row++;
            }

            // Create Excel table
            var tableRange = worksheet.Range(row - result.TableResults.Count - 1, 1, row - 1, 7);
            var excelTable = tableRange.CreateTable();
            excelTable.Theme = XLTableTheme.TableStyleLight9;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }

    private void AddTableDetailSheet(XLWorkbook workbook, TableComparisonResult tableResult)
    {
        var sheetName = SanitizeSheetName(tableResult.TableName);
        var worksheet = workbook.Worksheets.Add(sheetName);

        // Header
        worksheet.Cell(1, 1).Value = $"{tableResult.TableName} - Differences";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 12;

        // Summary
        var row = 3;
        worksheet.Cell(row++, 1).Value = $"Total Source: {tableResult.SourceRecordCount}";
        worksheet.Cell(row++, 1).Value = $"Total Target: {tableResult.TargetRecordCount}";
        worksheet.Cell(row++, 1).Value = $"New: {tableResult.NewCount}, Modified: {tableResult.ModifiedCount}, Deleted: {tableResult.DeletedCount}";
        
        row++; // Blank row

        // Detail table header
        worksheet.Cell(row, 1).Value = "Record ID";
        worksheet.Cell(row, 2).Value = "Record Name";
        worksheet.Cell(row, 3).Value = "Status";
        worksheet.Cell(row, 4).Value = "Field Name";
        worksheet.Cell(row, 5).Value = "Source Value";
        worksheet.Cell(row, 6).Value = "Target Value";

        var headerRow = worksheet.Range(row, 1, row, 6);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRow.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        row++;
        var startRow = row;

        // Detail data
        foreach (var difference in tableResult.Differences.OrderBy(d => d.DifferenceType).ThenBy(d => d.RecordName))
        {
            if (difference.DifferenceType == DifferenceType.Modified && difference.FieldDifferences.Any())
            {
                // Modified records - one row per field difference
                foreach (var fieldDiff in difference.FieldDifferences.OrderBy(f => f.FieldName))
                {
                    worksheet.Cell(row, 1).Value = difference.RecordId.ToString();
                    worksheet.Cell(row, 2).Value = difference.RecordName;
                    worksheet.Cell(row, 3).Value = "MODIFIED";
                    worksheet.Cell(row, 3).Style.Fill.BackgroundColor = XLColor.LightYellow;
                    worksheet.Cell(row, 4).Value = fieldDiff.FieldName;
                    worksheet.Cell(row, 5).Value = fieldDiff.SourceValue ?? "(null)";
                    worksheet.Cell(row, 6).Value = fieldDiff.TargetValue ?? "(null)";
                    row++;
                }
            }
            else
            {
                // New or Deleted records - single row
                worksheet.Cell(row, 1).Value = difference.RecordId.ToString();
                worksheet.Cell(row, 2).Value = difference.RecordName;
                worksheet.Cell(row, 3).Value = difference.DifferenceType.ToString().ToUpper();
                
                if (difference.DifferenceType == DifferenceType.New)
                {
                    worksheet.Cell(row, 3).Style.Fill.BackgroundColor = XLColor.LightGreen;
                }
                else if (difference.DifferenceType == DifferenceType.Deleted)
                {
                    worksheet.Cell(row, 3).Style.Fill.BackgroundColor = XLColor.LightCoral;
                }

                row++;
            }
        }

        // Create Excel table
        if (row > startRow)
        {
            var tableRange = worksheet.Range(startRow - 1, 1, row - 1, 6);
            var excelTable = tableRange.CreateTable();
            excelTable.Theme = XLTableTheme.TableStyleLight9;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }

    private string SanitizeSheetName(string name)
    {
        // Excel sheet names have max 31 chars and can't contain: \ / ? * [ ]
        var sanitized = name;
        foreach (var c in new[] { '\\', '/', '?', '*', '[', ']' })
        {
            sanitized = sanitized.Replace(c, '_');
        }

        if (sanitized.Length > 31)
        {
            sanitized = sanitized.Substring(0, 31);
        }

        return sanitized;
    }
}
