using System.CommandLine;
using PowerApps.CLI.Commands;

namespace PowerApps.CLI;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("PowerApps CLI - Utilities for PowerApps development");

        // Add schema-export command
        rootCommand.AddCommand(SchemaCommand.CreateCommand());

        // Add constants-generate command
        rootCommand.AddCommand(ConstantsCommand.CreateCommand());

        // Add refdata-compare command
        rootCommand.AddCommand(RefDataCompareCommand.CreateCommand());

        // Add process-manage command
        rootCommand.AddCommand(ProcessManageCommand.CreateCommand());

        return await rootCommand.InvokeAsync(args);
    }
}
