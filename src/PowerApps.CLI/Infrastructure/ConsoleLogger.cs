namespace PowerApps.CLI.Infrastructure;

/// <summary>
/// Logs messages to the console with color-coded output.
/// </summary>
public class ConsoleLogger : IConsoleLogger
{
    public bool IsVerboseEnabled { get; set; }

    public void LogInfo(string message)
    {
        WriteWithColor(message, ConsoleColor.White);
    }

    public void LogWarning(string message)
    {
        WriteWithColor(message, ConsoleColor.Yellow);
    }

    public void LogError(string message)
    {
        WriteWithColor(message, ConsoleColor.Red);
    }

    public void LogSuccess(string message)
    {
        WriteWithColor(message, ConsoleColor.Green);
    }

    public void LogVerbose(string message)
    {
        if (IsVerboseEnabled)
        {
            WriteWithColor(message, ConsoleColor.Gray);
        }
    }

    private static void WriteWithColor(string message, ConsoleColor color)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = previousColor;
    }
}
