# PowerApps.CLI

> **Note:** This project was developed with assistance from Claude Sonnet 4.5 (Anthropic AI).

A .NET command-line tool for extracting and exporting metadata schema from Microsoft Power Platform / Dynamics 365 environments.

## Features

- 🔍 **Schema Extraction** - Extract entity, attribute, and relationship metadata from Dataverse environments
- 🎯 **Solution Filtering** - Filter by one or multiple solutions (comma-separated)
- 📊 **Multiple Export Formats**:
  - **JSON** - Complete schema with full metadata
  - **XLSX** - Excel workbook with filterable tables and interactive navigation
- 🔐 **Flexible Authentication**:
  - Service Principal (Client ID/Secret)
  - Connection String
  - Interactive OAuth
  - Environment Variables
- ✅ **Audit Information** - Includes audit enablement status at entity and attribute levels
- 🔗 **Entity Deduplication** - Tracks which solutions contain each entity

## Installation

### Prerequisites

- .NET 8.0 SDK or later
- Access to a Power Platform / Dynamics 365 environment

### Build from Source

```bash
git clone https://github.com/yourusername/PowerApps.CLI.git
cd PowerApps.CLI
dotnet build -c Release
```

## Usage

### Using Pre-built Executable (Recommended)

After building or downloading a release, run directly:

```bash
# Windows
.\powerapps-cli.exe schema-export --url "https://yourorg.crm.dynamics.com" --output "schema.xlsx"

# Linux/macOS
./powerapps-cli schema-export --url "https://yourorg.crm.dynamics.com" --output "schema.xlsx"
```

### Using dotnet run (Development)

When developing or if you prefer to run from source:

```bash
dotnet run --project src/PowerApps.CLI -- schema-export --url "https://yourorg.crm.dynamics.com" --output "schema.xlsx"
```

### With Service Principal Authentication

```bash
powerapps-cli schema-export \
  --url "https://yourorg.crm.dynamics.com" \
  --client-id "your-client-id" \
  --client-secret "your-client-secret" \
  --solution "YourSolution" \
  --output "schema.xlsx" \
  --format xlsx
```

### Multiple Solutions

```bash
powerapps-cli schema-export \
  --url "https://yourorg.crm.dynamics.com" \
  --solution "Solution1,Solution2,Solution3" \
  --output "multi-solution-schema.json" \
  --format json
```

### Using Environment Variables

```bash
export DATAVERSE_CLIENT_ID="your-client-id"
export DATAVERSE_CLIENT_SECRET="your-client-secret"

powerapps-cli schema-export \
  --url "https://yourorg.crm.dynamics.com" \
  --output "schema.xlsx"
```

### Using Connection String

```bash
powerapps-cli schema-export \
  --connection-string "AuthType=ClientSecret;Url=https://yourorg.crm.dynamics.com;ClientId=...;ClientSecret=..." \
  --output "schema.json"
```

## Command Reference

### schema-export

Extracts metadata schema from PowerApps/Dataverse environments.

#### Options

| Option | Description | Required | Default |
|--------|-------------|----------|---------|
| `-u, --url` | PowerApps environment URL | Yes* | - |
| `-s, --solution` | Solution unique name(s) (comma-separated) | No | All entities |
| `-o, --output` | Output file path | No | `powerapp-schema.json` |
| `-f, --format` | Output format: `json` or `xlsx` | No | `json` |
| `-c, --connection-string` | Dataverse connection string | No | - |
| `--client-id` | Azure AD Application Client ID | No | - |
| `--client-secret` | Azure AD Application Client Secret | No | - |
| `-v, --verbose` | Enable verbose output | No | `false` |

\* Either `--url` or `--connection-string` must be provided.

## Output Formats

### JSON

Complete schema export with all metadata including:
- Entity definitions with audit settings
- Attribute metadata with types, constraints, and audit settings
- Relationships (1:N and N:N)
- OptionSets with all options
- Solution provenance information

### XLSX (Excel)

Interactive Excel workbook featuring:
- **Summary Sheet**: 
  - Environment and solution metadata
  - Filterable table of all entities
  - Clickable hyperlinks to entity detail sheets
- **Entity Detail Sheets**: One per entity with:
  - Entity properties and audit settings
  - Filterable table of attributes
- **Attributes Sheet**: Complete list of all attributes across all entities
- **Relationships Sheet**: All entity relationships

## Excel Features

The XLSX export includes:
- ✅ **Excel Tables** with filter dropdowns on all data sheets
- 🔗 **Interactive Navigation** - Click entity names to jump to detail sheets
- 📊 **Statistics** - Entity, attribute, and relationship counts
- 🎨 **Professional Formatting** - Color-coded headers and styled tables
- 🔍 **Audit Information** - "Is Audit Enabled" columns for entities and attributes

## Architecture

```
Commands/
  └── SchemaCommand.cs          # CLI command definition
Services/
  ├── SchemaService.cs          # Main orchestration service
  ├── SchemaExtractor.cs        # Metadata extraction with solution filtering
  ├── SchemaExporter.cs         # Export to JSON/XLSX formats
  └── MetadataMapper.cs         # SDK to model mapping
Infrastructure/
  ├── DataverseClient.cs        # Dataverse connection management
  ├── FileWriter.cs             # File I/O abstraction
  └── ConsoleLogger.cs          # Logging implementation
Models/
  ├── PowerAppsSchema.cs        # Root schema model
  ├── EntitySchema.cs           # Entity metadata
  ├── AttributeSchema.cs        # Attribute metadata
  ├── RelationshipSchema.cs     # Relationship metadata
  └── OptionSetSchema.cs        # OptionSet metadata
```

## Testing

The project includes comprehensive unit tests with over 100 test cases.

### Run Tests

```bash
dotnet test
```

### Run Tests with Coverage

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
reportgenerator -reports:"tests/PowerApps.CLI.Tests/TestResults/coverage.cobertura.xml" \
  -targetdir:"TestResults/CoverageReport" -reporttypes:"Html;TextSummary"
```

Current test coverage:
- Line coverage: 58.7%
- Branch coverage: 55.1%
- 100 passing tests

## Development

### Project Structure

- `src/PowerApps.CLI/` - Main application code
- `tests/PowerApps.CLI.Tests/` - Unit tests
- `test-scripts/` - Local test scripts (not committed)

### Dependencies

- **Microsoft.PowerPlatform.Dataverse.Client** - Dataverse SDK
- **ClosedXML** - Excel file generation
- **System.CommandLine** - CLI framework
- **xUnit** - Testing framework
- **Moq** - Mocking library

## Security Notes

⚠️ **Never commit credentials to source control**

The `.gitignore` is configured to exclude:
- Test scripts containing credentials (`test-scripts/*.ps1`)
- Generated schema files (`test-schema*.json`, `test-schema*.xlsx`)
- Coverage reports (`coverage/`)

Use environment variables or Azure Key Vault for production credentials.

## Contributing

Contributions are welcome! Please ensure:
- All tests pass
- New features include unit tests
- Code follows existing patterns and conventions

## License

MIT License - see [LICENSE](LICENSE) file for details.

This project is provided as-is with no warranties. Feel free to use, modify, and distribute as needed.
