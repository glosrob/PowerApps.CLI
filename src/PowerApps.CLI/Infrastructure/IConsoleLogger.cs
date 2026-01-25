namespace PowerApps.CLI.Infrastructure;

/// <summary>
/// Provides logging functionality to the console.
/// </summary>
public interface IConsoleLogger
{
    /// <summary>
    /// Logs an informational message.
    /// </summary>
    void LogInfo(string message);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    void LogWarning(string message);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    void LogError(string message);

    /// <summary>
    /// Logs a success message.
    /// </summary>
    void LogSuccess(string message);

    /// <summary>
    /// Logs a verbose/debug message.
    /// </summary>
    void LogVerbose(string message);

    /// <summary>
    /// Logs an informational message only if verbose mode is enabled.
    /// </summary>
    void LogInfoIfVerbose(string message);

    /// <summary>
    /// Logs a warning message only if verbose mode is enabled.
    /// </summary>
    void LogWarningIfVerbose(string message);

    /// <summary>
    /// Logs a success message only if verbose mode is enabled.
    /// </summary>
    void LogSuccessIfVerbose(string message);

    /// <summary>
    /// Gets or sets whether verbose logging is enabled.
    /// </summary>
    bool IsVerboseEnabled { get; set; }
}
