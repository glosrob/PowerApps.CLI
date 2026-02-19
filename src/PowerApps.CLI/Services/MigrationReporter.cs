using ClosedXML.Excel;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

public class MigrationReporter : IMigrationReporter
{
    private readonly IFileWriter _fileWriter;

    public MigrationReporter(IFileWriter fileWriter)
    {
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public async Task GenerateReportAsync(MigrationSummary summary, string outputPath)
    {
        using var workbook = new XLWorkbook();

        // Summary sheet
        var summarySheet = workbook.Worksheets.Add("Migration Summary");
        BuildSummarySheet(summarySheet, summary);

        // Errors sheet (if any)
        if (summary.HasErrors)
        {
            var errorsSheet = workbook.Worksheets.Add("Errors");
            BuildErrorsSheet(errorsSheet, summary);
        }

        // Auto-fit columns on all sheets
        foreach (var ws in workbook.Worksheets)
        {
            ws.Columns().AdjustToContents();
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        await _fileWriter.WriteBytesAsync(outputPath, stream.ToArray());
    }

    private void BuildSummarySheet(IXLWorksheet worksheet, MigrationSummary summary)
    {
        // Header
        worksheet.Cell(1, 1).Value = "Migration Report";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        // Metadata
        var row = 3;
        worksheet.Cell(row, 1).Value = "Source Environment:";
        worksheet.Cell(row++, 2).Value = summary.SourceEnvironment;
        worksheet.Cell(row, 1).Value = "Target Environment:";
        worksheet.Cell(row++, 2).Value = summary.TargetEnvironment;
        worksheet.Cell(row, 1).Value = "Execution Date:";
        worksheet.Cell(row++, 2).Value = summary.ExecutionDate.ToString("yyyy-MM-dd HH:mm:ss UTC");
        worksheet.Cell(row, 1).Value = "Mode:";
        worksheet.Cell(row++, 2).Value = summary.IsDryRun ? "Dry Run (Preview)" : "Executed";
        worksheet.Cell(row, 1).Value = "Duration:";
        worksheet.Cell(row++, 2).Value = summary.Duration.ToString(@"mm\:ss\.fff");

        row++; // Blank row

        // Totals
        worksheet.Cell(row, 1).Value = "Totals:";
        worksheet.Cell(row++, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Value = "Total Records:";
        worksheet.Cell(row++, 2).Value = summary.TotalRecords;
        worksheet.Cell(row, 1).Value = "Upserted:";
        worksheet.Cell(row++, 2).Value = summary.TotalUpserted;
        worksheet.Cell(row, 1).Value = "Lookups Patched:";
        worksheet.Cell(row++, 2).Value = summary.TotalLookupsPatched;
        worksheet.Cell(row, 1).Value = "State Changes:";
        worksheet.Cell(row++, 2).Value = summary.TotalStateChanges;
        worksheet.Cell(row, 1).Value = "Skipped (unchanged):";
        worksheet.Cell(row++, 2).Value = summary.TotalSkipped;
        worksheet.Cell(row, 1).Value = "N:N Associated:";
        worksheet.Cell(row++, 2).Value = summary.TotalAssociated;
        worksheet.Cell(row, 1).Value = "N:N Disassociated:";
        worksheet.Cell(row++, 2).Value = summary.TotalDisassociated;
        worksheet.Cell(row, 1).Value = "Errors:";
        worksheet.Cell(row, 2).Value = summary.TotalErrors;
        if (summary.HasErrors)
        {
            worksheet.Cell(row, 2).Style.Font.FontColor = XLColor.Red;
        }
        row++;

        row++; // Blank row

        // Table details header
        worksheet.Cell(row, 1).Value = "Table";
        worksheet.Cell(row, 2).Value = "Records";
        worksheet.Cell(row, 3).Value = "Upserted";
        worksheet.Cell(row, 4).Value = "Lookups Patched";
        worksheet.Cell(row, 5).Value = "State Changes";
        worksheet.Cell(row, 6).Value = "Skipped";
        worksheet.Cell(row, 7).Value = "Errors";

        var headerRange = worksheet.Range(row, 1, row, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        row++;

        // Table rows
        var dataStartRow = row;
        foreach (var tableResult in summary.TableResults)
        {
            worksheet.Cell(row, 1).Value = tableResult.TableName;
            worksheet.Cell(row, 2).Value = tableResult.RecordCount;
            worksheet.Cell(row, 3).Value = tableResult.UpsertedCount;
            worksheet.Cell(row, 4).Value = tableResult.LookupsPatchedCount;
            worksheet.Cell(row, 5).Value = tableResult.StateChangesCount;
            worksheet.Cell(row, 6).Value = tableResult.SkippedCount;
            worksheet.Cell(row, 7).Value = tableResult.Errors.Count;

            if (tableResult.Errors.Count > 0)
            {
                worksheet.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
            }

            row++;
        }

        // Create table
        if (summary.TableResults.Any())
        {
            var tableRange = worksheet.Range(dataStartRow - 1, 1, row - 1, 7);
            var table = tableRange.CreateTable();
            table.Theme = XLTableTheme.TableStyleLight9;
        }

        // N:N Relationship details
        if (summary.ManyToManyResults.Any())
        {
            row++; // Blank row

            worksheet.Cell(row, 1).Value = "N:N Relationships";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;

            worksheet.Cell(row, 1).Value = "Relationship";
            worksheet.Cell(row, 2).Value = "Entity 1";
            worksheet.Cell(row, 3).Value = "Entity 2";
            worksheet.Cell(row, 4).Value = "Source Count";
            worksheet.Cell(row, 5).Value = "Associated";
            worksheet.Cell(row, 6).Value = "Disassociated";
            worksheet.Cell(row, 7).Value = "Errors";

            var m2mHeaderRange = worksheet.Range(row, 1, row, 7);
            m2mHeaderRange.Style.Font.Bold = true;
            m2mHeaderRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            var m2mStartRow = row;
            row++;

            foreach (var m2mResult in summary.ManyToManyResults)
            {
                worksheet.Cell(row, 1).Value = m2mResult.RelationshipName;
                worksheet.Cell(row, 2).Value = m2mResult.Entity1Name;
                worksheet.Cell(row, 3).Value = m2mResult.Entity2Name;
                worksheet.Cell(row, 4).Value = m2mResult.SourceCount;
                worksheet.Cell(row, 5).Value = m2mResult.AssociatedCount;
                worksheet.Cell(row, 6).Value = m2mResult.DisassociatedCount;
                worksheet.Cell(row, 7).Value = m2mResult.Errors.Count;

                if (m2mResult.Errors.Count > 0)
                {
                    worksheet.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
                }
                row++;
            }

            var m2mTableRange = worksheet.Range(m2mStartRow, 1, row - 1, 7);
            var m2mTable = m2mTableRange.CreateTable();
            m2mTable.Theme = XLTableTheme.TableStyleLight9;
        }
    }

    private void BuildErrorsSheet(IXLWorksheet worksheet, MigrationSummary summary)
    {
        // Header
        worksheet.Cell(1, 1).Value = "Table";
        worksheet.Cell(1, 2).Value = "Record ID";
        worksheet.Cell(1, 3).Value = "Phase";
        worksheet.Cell(1, 4).Value = "Error Message";

        var headerRange = worksheet.Range(1, 1, 1, 4);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        var row = 2;
        var allErrors = summary.TableResults.SelectMany(t => t.Errors)
            .Concat(summary.ManyToManyResults.SelectMany(r => r.Errors));
        foreach (var error in allErrors)
        {
            worksheet.Cell(row, 1).Value = error.TableName;
            worksheet.Cell(row, 2).Value = error.RecordId.ToString();
            worksheet.Cell(row, 3).Value = error.Phase;
            worksheet.Cell(row, 4).Value = error.ErrorMessage;
            row++;
        }

        // Create table
        if (row > 2)
        {
            var tableRange = worksheet.Range(1, 1, row - 1, 4);
            var table = tableRange.CreateTable();
            table.Theme = XLTableTheme.TableStyleLight9;
        }
    }
}
