# PowerApps.CLI - Copilot Instructions

> **Note:** This project was developed with assistance from Claude Sonnet 4.5 (Anthropic AI).

## Project Overview

PowerApps.CLI is a .NET 8.0 command-line tool for extracting metadata and generating strongly-typed constants from Microsoft Power Platform / Dynamics 365 (Dataverse) environments.

### Core Commands

1. **`schema-export`** - Extract entity, attribute, and relationship metadata
   - Outputs: JSON or Excel (XLSX) formats
   - Supports solution filtering and deduplication
   
2. **`constants-generate`** - Generate C# constants from Dataverse metadata
   - Creates strongly-typed classes for Tables and Choices (modern terminology)
   - Single-file or multi-file output modes
   - Smart filtering by solution, prefix, entity, or attribute

## Project Structure

```
src/PowerApps.CLI/
├── Commands/           # Command-line command handlers (System.CommandLine)
│   ├── SchemaCommand.cs       # schema-export command
│   └── ConstantsCommand.cs    # constants-generate command
├── Infrastructure/     # External dependencies & I/O
│   ├── DataverseClient.cs     # Dataverse API wrapper
│   ├── ConsoleLogger.cs       # Console output
│   └── FileWriter.cs          # File system operations
├── Models/            # Domain models & DTOs
│   ├── *Schema.cs            # Metadata models
│   └── ConstantsConfig.cs    # Configuration model
└── Services/          # Business logic
    ├── SchemaExtractor.cs     # Extract metadata from Dataverse
    ├── ConstantsGenerator.cs  # Generate C# code
    ├── ConstantsFilter.cs     # Filter logic
    └── CodeTemplateGenerator.cs # C# code generation

tests/PowerApps.CLI.Tests/  # Unit tests (xUnit)
Generated/                   # Example output from constants-generate
test-scripts/               # PowerShell test/demo scripts
```

## Technology Stack

- **.NET 8.0** - Target framework
- **System.CommandLine** (beta) - CLI framework
- **Microsoft.PowerPlatform.Dataverse.Client** - Dataverse connectivity
- **ClosedXML** - Excel file generation
- **xUnit** - Unit testing
- **Coverlet** - Code coverage

## Architecture & Design Patterns

### Dependency Injection
- Uses `Microsoft.Extensions.DependencyInjection`
- All services registered in [Program.cs](../src/PowerApps.CLI/Program.cs)
- Interface-based design for testability (I-prefixed interfaces)

### Authentication Methods (Priority Order)
1. Command-line arguments (--client-id/--client-secret)
2. JSON configuration file (--config)
3. Connection string (--connection-string)
4. Environment variables (DATAVERSE_URL, DATAVERSE_CLIENT_ID, etc.)
5. Interactive OAuth (fallback)

### Naming Conventions
- **Modern Dataverse Terminology**: Tables (not Entities), Choices (not OptionSets)
- **Generated Code**: Uses `DisplayName` for class names, strips publisher prefixes
- **File Naming**: kebab-case for executables (`powerapps-cli`), PascalCase for C# files

## Code Generation Details

### Constants Generation Flow
1. **Extract** metadata via SchemaExtractor
2. **Filter** based on config (ConstantsFilter)
3. **Generate** C# code (CodeTemplateGenerator)
4. **Write** files (FileWriter)

### Output Modes
- **Single File**: `Tables.cs` + `Choices.cs` (default)
- **Multi File**: `Tables/*.cs` + `Choices/*.cs` (--multi-file flag)

### Generated Code Features
- Nested classes for logical organization
- XML documentation comments
- Metadata comments (e.g., `// LogicalName: cr123_fieldname`)
- Proper C# identifier sanitization

## Testing

### Running Tests
```powershell
# All tests
.\test-scripts\run-tests.ps1

# With coverage report
.\test-scripts\run-coverage.ps1
```

### Test Structure
- Unit tests in `tests/PowerApps.CLI.Tests/`
- Mock-based testing for external dependencies
- Coverage reports generated to `coverage/report/`

### Testing with Real Dataverse
- Test scripts in `test-scripts/` (credential scripts are git-ignored)
- Never commit files with real credentials
- Use `sample-constants-config.json` as a template

## Important Conventions

### DO ✅
- Use interfaces for dependency injection
- Follow existing namespace patterns (`PowerApps.CLI.*`)
- Add XML comments to public APIs
- Write unit tests for new functionality
- Use modern Dataverse terminology (Tables/Choices)
- Keep Infrastructure layer isolated from business logic
- Use the existing IConsoleLogger for user output

### DON'T ❌
- Commit credential test scripts (they're git-ignored)
- Mix old terminology (Entities/OptionSets) with new
- Add console output directly (use IConsoleLogger)
- Skip unit tests for new features
- Break existing command-line interface compatibility

## Common Workflows

### Adding a New Command
1. Create command class in `Commands/`
2. Implement handler using System.CommandLine
3. Register in [Program.cs](../src/PowerApps.CLI/Program.cs)
4. Add tests in corresponding test file
5. Update README.md with usage examples

### Modifying Generated Code
- Edit templates in `CodeTemplateGenerator.cs`
- Regenerate examples: `dotnet run -- constants-generate --config test-scripts/sample-constants-config.json`
- Review changes in `Generated/` folder
- Ensure backwards compatibility

### Adding a New Export Format
1. Add format enum value
2. Implement writer service (e.g., `CsvWriter.cs`)
3. Update `SchemaCommand.cs` handler
4. Add tests
5. Document in README.md

## Known Considerations

- **System.CommandLine** is still in beta - API may change
- **Publisher Prefixes** are stripped from DisplayName-based class names
- **Excel Output** uses ClosedXML (may have memory implications for large schemas)
- **Authentication** errors are common - check credentials and environment URL format
- **Solution Filtering** supports comma-separated values for multiple solutions

## Build & Release

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Run from source
dotnet run --project src/PowerApps.CLI -- [command] [options]

# Executable location after build
src/PowerApps.CLI/bin/Release/net8.0/powerapps-cli.exe
```

## Coverage Reports

Coverage reports are generated to `coverage/report/index.html` using:
```powershell
.\test-scripts\run-coverage.ps1
```

View the report by opening the HTML file in a browser.

## Additional Resources

- [README.md](../README.md) - User documentation and examples
- [test-scripts/README.md](../test-scripts/README.md) - Test script documentation
- Solution file: [PowerApps.CLI.sln](../PowerApps.CLI.sln)
