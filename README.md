# PowerApps.CLI

> **Note:** This project was developed with assistance from Claude Sonnet 4.5 (Anthropic AI).

A .NET command-line tool for extracting and exporting metadata schema from Microsoft Power Platform / Dynamics 365 environments.

## Features

### Schema Extraction
- 🔍 **Metadata Export** - Extract entity, attribute, and relationship metadata from Dataverse environments
- 🎯 **Solution Filtering** - Filter by one or multiple solutions (comma-separated)
- 📊 **Multiple Export Formats**:
  - **JSON** - Complete schema with full metadata
  - **XLSX** - Excel workbook with filterable tables and interactive navigation
- ✅ **Audit Information** - Includes audit enablement status at entity and attribute levels
- 🔗 **Entity Deduplication** - Tracks which solutions contain each entity

### Constants Generation
- 🎨 **C# Constants** - Generate strongly-typed C# constants from Dataverse metadata
- 📋 **Tables & Choices** - Modern terminology (Tables instead of Entities, Choices instead of OptionSets)
- 🗂️ **Flexible Output**:
  - Single file mode: Tables.cs and Choices.cs
  - Multiple files mode: Tables/*.cs and Choices/*.cs
- 🎯 **Smart Filtering**:
  - Solution-based filtering
  - Entity exclusions
  - Attribute exclusions
  - Prefix-based filtering
- 🏷️ **Clean Naming** - Uses DisplayName for readable class names without publisher prefixes
- 📝 **Rich Documentation** - XML comments and metadata comments in generated code

### Reference Data Comparison
- 🔄 **Environment Comparison** - Compare reference data tables between source and target environments
- 📊 **Difference Detection** - Identifies new, modified, and deleted records
- 🎯 **Bidirectional Analysis** - Compares both ways to find orphaned records
- 📝 **Smart Field Handling** - Uses FormattedValues for human-readable lookups and option sets
- 🔍 **Flexible Filtering** - Per-table FetchXML filters and field exclusions
- 📑 **Excel Reports** - Interactive Excel workbooks with summary and detail sheets
- ⚙️ **Built-in Defaults** - Automatically excludes system fields (createdby, modifiedby, etc.)

### Authentication
- 🔐 **Multiple Auth Methods**:
  - Service Principal (Client ID/Secret)
  - Connection String
  - Interactive OAuth
  - Environment Variables

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

### Schema Export

Extract metadata schema from Dataverse environments.

#### Using Pre-built Executable (Recommended)

After building or downloading a release, run directly:

```bash
# Windows
.\powerapps-cli.exe schema-export --url "https://yourorg.crm.dynamics.com" --output "schema.xlsx"

# Linux/macOS
./powerapps-cli schema-export --url "https://yourorg.crm.dynamics.com" --output "schema.xlsx"
```

#### Using dotnet run (Development)

When developing or if you prefer to run from source:

```bash
dotnet run --project src/PowerApps.CLI -- schema-export --url "https://yourorg.crm.dynamics.com" --output "schema.xlsx"
```

#### With Service Principal Authentication

```bash
powerapps-cli schema-export \
  --url "https://yourorg.crm.dynamics.com" \
  --client-id "your-client-id" \
  --client-secret "your-client-secret" \
  --solution "YourSolution" \
  --output "schema.xlsx" \
  --format xlsx
```

#### Multiple Solutions

```bash
powerapps-cli schema-export \
  --url "https://yourorg.crm.dynamics.com" \
  --solution "Solution1,Solution2,Solution3" \
  --output "multi-solution-schema.json" \
  --format json
```

### Constants Generation

Generate C# constants from Dataverse metadata.

#### Basic Usage

```bash
powerapps-cli constants-generate \
  --url "https://yourorg.crm.dynamics.com" \
  --solution "YourSolution" \
  --namespace "MyCompany.Model" \
  --output "./Generated"
```

#### Using Connection String

```bash
powerapps-cli constants-generate \
  --connection-string "AuthType=ClientSecret;Url=https://yourorg.crm.dynamics.com;ClientId=...;ClientSecret=..." \
  --solution "YourSolution" \
  --namespace "MyCompany.Model" \
  --output "./Generated"
```

#### Single File Mode

```bash
powerapps-cli constants-generate \
  --url "https://yourorg.crm.dynamics.com" \
  --solution "YourSolution" \
  --namespace "MyCompany.Model" \
  --output "./Generated" \
  --single-file
```

#### With Filtering

```bash
powerapps-cli constants-generate \
  --url "https://yourorg.crm.dynamics.com" \
  --solution "YourSolution" \
  --namespace "MyCompany.Model" \
  --output "./Generated" \
  --exclude-entities "systemuser,team" \
  --exclude-attributes "createdon,modifiedon,createdby,modifiedby" \
  --attribute-prefix "rob_"
```

#### Using Configuration File

```bash
powerapps-cli constants-generate \
  --url "https://yourorg.crm.dynamics.com" \
  --solution "YourSolution" \
  --config "./constants-config.json"
```

Example configuration file:
```json
{
  "SingleFile": false,
  "IncludeEntities": true,
  "IncludeGlobalOptionSets": true,
  "IncludeComments": true,
  "IncludeRelationships": true,
  "PascalCaseConversion": true,
  "AttributePrefix": "rob_",
  "ExcludeAttributes": ["createdon", "modifiedon", "createdby", "modifiedby"],
  "ExcludeEntities": ["systemuser", "team"]
}
```

### Reference Data Comparison

Compare reference data between source and target environments.

#### Basic Usage

```bash
powerapps-cli refdata-compare \
  --config refdata-config.json \
  --source-url "https://dev.crm.dynamics.com" \
  --target-url "https://test.crm.dynamics.com" \
  --client-id "$CLIENT_ID" \
  --client-secret "$CLIENT_SECRET" \
  --output dev-vs-test.xlsx
```

#### Using Connection Strings

```bash
powerapps-cli refdata-compare \
  --config refdata-config.json \
  --source-connection "$DEV_CONNECTION_STRING" \
  --target-connection "$TEST_CONNECTION_STRING" \
  --output dev-vs-test.xlsx
```

#### Example Config File

```json
{
  "excludeSystemFields": true,
  "globalExcludeFields": ["custom_ignorefield"],
  "tables": [
    {
      "logicalName": "rob_category",
      "primaryIdField": "rob_categoryid",
      "primaryNameField": "rob_name",
      "filter": "<filter><condition attribute='statecode' operator='eq' value='0'/></filter>",
      "excludeFields": []
    },
    {
      "logicalName": "rob_priority",
      "primaryIdField": "rob_priorityid",
      "primaryNameField": "rob_priorityname",
      "filter": "<filter><condition attribute='statecode' operator='eq' value='0'/></filter>",
      "excludeFields": ["rob_temporaryfield"]
    }
  ]
}
```

**Output**: Excel workbook with:
- Summary sheet showing all tables and difference counts
- Detail sheets for each table with differences (NEW/MODIFIED/DELETED records)
- Field-level comparison using formatted values (human-readable lookups and option sets)

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
| `--attribute-prefix` | Only include attributes with this prefix | No | - |
| `--exclude-attributes` | Comma-separated attribute names to exclude | No | - |

\* Either `--url` or `--connection-string` must be provided.

### constants-generate

Generates C# constants from Dataverse metadata.

#### Options

| Option | Description | Required | Default |
|--------|-------------|----------|---------|
| `-u, --url` | PowerApps environment URL | Yes* | - |
| `-s, --solution` | Solution unique name(s) to filter by | No | All entities |
| `-o, --output` | Output directory path | No | `./Generated` |
| `-n, --namespace` | Root namespace for generated code | Yes | - |
| `--single-file` | Generate single Tables.cs and Choices.cs files | No | `false` |
| `--config` | Path to JSON configuration file | No | - |
| `-c, --connection-string` | Dataverse connection string | No | - |
| `--client-id` | Azure AD Application Client ID | No | - |
| `--client-secret` | Azure AD Application Client Secret | No | - |
| `-v, --verbose` | Enable verbose output | No | `false` |
| `--include-entities` | Include entity constants (Tables) | No | `true` |
| `--include-optionsets` | Include option set constants (Choices) | No | `true` |
| `--exclude-entities` | Comma-separated entity logical names to exclude | No | - |
| `--exclude-attributes` | Comma-separated attribute logical names to exclude | No | - |
| `--attribute-prefix` | Only include attributes with this prefix | No | - |
| `--pascal-case` | Convert identifiers to PascalCase | No | `true` |

\* Either `--url` or `--connection-string` must be provided.

### refdata-compare

Compares reference data between source and target Dataverse environments.

#### Options

| Option | Description | Required | Default |
|--------|-------------|----------|---------|
| `--config` | Path to JSON configuration file | Yes | - |
| `--source-url` | Source environment URL | Yes* | - |
| `--target-url` | Target environment URL | Yes* | - |
| `--source-connection` | Source environment connection string | No | - |
| `--target-connection` | Target environment connection string | No | - |
| `--client-id` | Azure AD Client ID (for both environments) | No | - |
| `--client-secret` | Azure AD Client Secret (for both environments) | No | - |
| `-o, --output` | Output Excel file path | No | `refdata-comparison.xlsx` |
| `-v, --verbose` | Enable verbose output | No | `false` |

\* Either `--source-url`/`--target-url` or `--source-connection`/`--target-connection` must be provided.

## Output Formats

### Schema Export

#### JSON

Complete schema export with all metadata including:
- Entity definitions with audit settings
- Attribute metadata with types, constraints, and audit settings
- Relationships (1:N and N:N)
- OptionSets with all options
- Solution provenance information

#### XLSX (Excel)

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

The XLSX export includes:
- ✅ **Excel Tables** with filter dropdowns on all data sheets
- 🔗 **Interactive Navigation** - Click entity names to jump to detail sheets
- 📊 **Statistics** - Entity, attribute, and relationship counts
- 🎨 **Professional Formatting** - Color-coded headers and styled tables
- 🔍 **Audit Information** - "Is Audit Enabled" columns for entities and attributes

### Constants Generation

#### Multiple Files Mode (Default)

Generated structure:
```
Generated/
├── Tables/
│   ├── Account.cs
│   ├── Contact.cs
│   └── ... (one file per entity)
└── Choices/
    ├── AccountType.cs
    ├── StatusCode.cs
    └── ... (one file per global option set)
```

Example generated file:
```csharp
namespace MyCompany.Model.Tables
{
    /// <summary>
    /// Constants for the Account entity.
    /// </summary>
    public static class Account
    {
        /// <summary>
        /// Logical name of the entity.
        /// </summary>
        public const string EntityLogicalName = "account";

        /// <summary>
        /// Primary ID attribute.
        /// </summary>
        public const string PrimaryIdAttribute = "accountid";

        /// <summary>
        /// name (String) - MaxLength: 160
        /// </summary>
        public const string Name = "name";

        /// <summary>
        /// accountcategorycode (Picklist) - Uses local option set
        /// </summary>
        public const string Category = "accountcategorycode";

        /// <summary>
        /// Category option set values.
        /// </summary>
        public static class CategoryOptions
        {
            /// <summary>
            /// Preferred Customer
            /// </summary>
            public const int PreferredCustomer = 1;

            /// <summary>
            /// Standard
            /// </summary>
            public const int Standard = 2;
        }
    }
}
```

#### Single File Mode

Generates two files:
- `Tables.cs` - All entity constants in one file
- `Choices.cs` - All global option set constants in one file

## Architecture

```
Commands/
  ├── SchemaCommand.cs          # Schema export CLI command
  └── ConstantsCommand.cs       # Constants generation CLI command
Services/
  ├── SchemaService.cs          # Schema export orchestration
  ├── SchemaExtractor.cs        # Metadata extraction with solution filtering
  ├── SchemaExporter.cs         # Export to JSON/XLSX formats
  ├── ConstantsGenerator.cs     # Constants generation orchestration
  ├── CodeTemplateGenerator.cs  # C# code template generation
  ├── ConstantsFilter.cs        # Entity/attribute filtering logic
  ├── IdentifierFormatter.cs    # C# identifier formatting (PascalCase, sanitization)
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
  ├── OptionSetSchema.cs        # OptionSet metadata
  ├── ConstantsConfig.cs        # Constants generation configuration
  └── ConstantsOutputConfig.cs  # Constants output settings
```

## Testing

The project includes comprehensive unit tests covering both schema extraction and constants generation.

### Run Tests

```bash
dotnet test
```

### Run Tests with Coverage

```bash
# Using test-scripts helper
.\test-scripts\run-coverage.ps1

# Or manually
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
reportgenerator -reports:"tests/PowerApps.CLI.Tests/TestResults/coverage.cobertura.xml" \
  -targetdir:"TestResults/CoverageReport" -reporttypes:"Html;TextSummary"
```

Current test coverage:
- **162 passing tests** (100% pass rate)
- Line coverage: 60%+
- Branch coverage: 55%+

Test coverage includes:
- ✅ Schema extraction and export (JSON/XLSX)
- ✅ Constants generation (single/multiple file modes)
- ✅ Code template generation
- ✅ Identifier formatting and sanitization
- ✅ Entity/attribute filtering
- ✅ Metadata mapping
- ✅ Model validation

## Development

### Project Structure

- `src/PowerApps.CLI/` - Main application code
  - `Commands/` - CLI command definitions
  - `Services/` - Business logic and orchestration
  - `Infrastructure/` - External integrations and utilities
  - `Models/` - Data models and schemas
- `tests/PowerApps.CLI.Tests/` - Unit tests
- `test-scripts/` - Local test scripts with sample usage (not committed)

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
- Generated constants output (`Generated/`)
- Generated schema files (`test-schema*.json`, `test-schema*.xlsx`)
- Coverage reports (`coverage/`, `TestResults/`)

Use environment variables or Azure Key Vault for production credentials.

## Contributing

Contributions are welcome! Please ensure:
- All tests pass
- New features include unit tests
- Code follows existing patterns and conventions

## License

MIT License - see [LICENSE](LICENSE) file for details.

This project is provided as-is with no warranties. Feel free to use, modify, and distribute as needed.
