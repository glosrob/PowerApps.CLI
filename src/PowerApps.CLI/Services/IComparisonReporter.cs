using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Generates Excel reports from comparison results.
/// </summary>
public interface IComparisonReporter
{
    /// <summary>
    /// Generates an Excel report from comparison results.
    /// </summary>
    /// <param name="result">The comparison result to report.</param>
    /// <param name="outputPath">Path to save the Excel file.</param>
    Task GenerateReportAsync(ComparisonResult result, string outputPath);
}
