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

        // Add detail sheets only for tables with differences, sorted alphabetically by TableName
        var sortedTableResults = result.TableResults
            .Where(t => t.HasDifferences)
            .OrderBy(t => t.TableName)
            .ToList();

        foreach (var tableResult in sortedTableResults)
        {
            AddTableDetailSheet(workbook, tableResult);
        }

        // Add detail sheets for relationships with differences, sorted alphabetically
        var sortedRelResults = result.RelationshipResults
            .Where(r => r.HasDifferences)
            .OrderBy(r => r.RelationshipName)
            .ToList();

        foreach (var relResult in sortedRelResults)
        {
            AddRelationshipDetailSheet(workbook, relResult);
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
            worksheet.Cell(row, 1).Value = "No differences found - all tables and relationships are in sync.";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontColor = XLColor.Green;
        }
        else
        {
            // Table summary section
            if (result.TableResults.Count > 0)
            {
                worksheet.Cell(row, 1).Value = "Table Comparisons";
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                worksheet.Cell(row, 1).Style.Font.FontSize = 11;
                row++;

                worksheet.Cell(row, 1).Value = "Table";
                worksheet.Cell(row, 2).Value = "Source Count";
                worksheet.Cell(row, 3).Value = "Target Count";
                worksheet.Cell(row, 4).Value = "New";
                worksheet.Cell(row, 5).Value = "Modified";
                worksheet.Cell(row, 6).Value = "Deleted";
                worksheet.Cell(row, 7).Value = "Status";

                var headerRow = worksheet.Range(row, 1, row, 7);
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

                row++;

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

                var tableRange = worksheet.Range(row - result.TableResults.Count - 1, 1, row - 1, 7);
                var excelTable = tableRange.CreateTable("TableComparisons");
                excelTable.Theme = XLTableTheme.TableStyleLight9;
            }

            // Relationship summary section
            if (result.RelationshipResults.Count > 0)
            {
                row++; // Blank row
                worksheet.Cell(row, 1).Value = "Relationship Comparisons";
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                worksheet.Cell(row, 1).Style.Font.FontSize = 11;
                row++;

                worksheet.Cell(row, 1).Value = "Relationship";
                worksheet.Cell(row, 2).Value = "Source Count";
                worksheet.Cell(row, 3).Value = "Target Count";
                worksheet.Cell(row, 4).Value = "New";
                worksheet.Cell(row, 5).Value = "Deleted";
                worksheet.Cell(row, 6).Value = "Status";

                var relHeaderRow = worksheet.Range(row, 1, row, 6);
                relHeaderRow.Style.Font.Bold = true;
                relHeaderRow.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

                row++;

                foreach (var relResult in result.RelationshipResults.OrderBy(r => r.RelationshipName))
                {
                    worksheet.Cell(row, 1).Value = relResult.RelationshipName;
                    worksheet.Cell(row, 2).Value = relResult.SourceAssociationCount;
                    worksheet.Cell(row, 3).Value = relResult.TargetAssociationCount;
                    worksheet.Cell(row, 4).Value = relResult.NewCount;
                    worksheet.Cell(row, 5).Value = relResult.DeletedCount;

                    var status = relResult.HasDifferences ? "Differences Found" : "In Sync";
                    worksheet.Cell(row, 6).Value = status;

                    if (relResult.HasDifferences)
                    {
                        worksheet.Cell(row, 6).Style.Font.FontColor = XLColor.Red;

                        var detailSheetName = SanitizeSheetName(relResult.RelationshipName);
                        worksheet.Cell(row, 1).SetHyperlink(new XLHyperlink($"'{detailSheetName}'!A1"));
                        worksheet.Cell(row, 1).Style.Font.FontColor = XLColor.Blue;
                        worksheet.Cell(row, 1).Style.Font.Underline = XLFontUnderlineValues.Single;
                    }
                    else
                    {
                        worksheet.Cell(row, 6).Style.Font.FontColor = XLColor.Green;
                    }

                    row++;
                }

                var relTableRange = worksheet.Range(row - result.RelationshipResults.Count - 1, 1, row - 1, 6);
                var relExcelTable = relTableRange.CreateTable("RelationshipComparisons");
                relExcelTable.Theme = XLTableTheme.TableStyleLight9;
            }
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
                    worksheet.Cell(row, 3).Value = "Modified";
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
                worksheet.Cell(row, 3).Value = difference.DifferenceType.ToString();

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

    private void AddRelationshipDetailSheet(XLWorkbook workbook, RelationshipComparisonResult relResult)
    {
        var sheetName = SanitizeSheetName(relResult.RelationshipName);
        var worksheet = workbook.Worksheets.Add(sheetName);

        // Header
        worksheet.Cell(1, 1).Value = $"{relResult.RelationshipName} - Differences";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 12;

        // Summary
        var row = 3;
        worksheet.Cell(row++, 1).Value = $"Intersect Entity: {relResult.IntersectEntity}";
        worksheet.Cell(row++, 1).Value = $"Total Source: {relResult.SourceAssociationCount}";
        worksheet.Cell(row++, 1).Value = $"Total Target: {relResult.TargetAssociationCount}";
        worksheet.Cell(row++, 1).Value = $"New: {relResult.NewCount}, Deleted: {relResult.DeletedCount}";

        row++; // Blank row

        // Detail table header
        worksheet.Cell(row, 1).Value = "Entity 1 Name";
        worksheet.Cell(row, 2).Value = "Entity 1 ID";
        worksheet.Cell(row, 3).Value = "Entity 2 Name";
        worksheet.Cell(row, 4).Value = "Entity 2 ID";
        worksheet.Cell(row, 5).Value = "Status";

        var headerRow = worksheet.Range(row, 1, row, 5);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        row++;
        var startRow = row;

        // Detail data
        foreach (var diff in relResult.Differences
            .OrderBy(d => d.DifferenceType)
            .ThenBy(d => d.Entity1Name)
            .ThenBy(d => d.Entity2Name))
        {
            worksheet.Cell(row, 1).Value = diff.Entity1Name ?? diff.Entity1Id.ToString();
            worksheet.Cell(row, 2).Value = diff.Entity1Id.ToString();
            worksheet.Cell(row, 3).Value = diff.Entity2Name ?? diff.Entity2Id.ToString();
            worksheet.Cell(row, 4).Value = diff.Entity2Id.ToString();
            worksheet.Cell(row, 5).Value = diff.DifferenceType.ToString();
            row++;
        }

        // Create Excel table
        if (row > startRow)
        {
            var tableRange = worksheet.Range(startRow - 1, 1, row - 1, 5);
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
