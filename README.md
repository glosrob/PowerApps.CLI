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
- 📝 **Rich Documentation** - XML comments and metadata comments in generated code

### Reference Data Comparison
- 🔄 **Environment Comparison** - Compare reference data tables between source and target environments
- 📊 **Difference Detection** - Identifies new, modified, and deleted records
- 🎯 **Bidirectional Analysis** - Compares both ways to find orphaned records

### Reference Data Migration
- 🚀 **Environment-to-Environment Migration** - Migrate reference data from source to target Dataverse environment
- 🔄 **Diff Mode** - Only pushes records that have changed (default), with `--force` to push all
- 🔗 **Multi-pass Strategy** - Handles self-referential lookups via a two-pass upsert approach
- ⚙️ **State Management** - Optionally sync record active/inactive state
- 🔗 **N:N Relationships** - Sync many-to-many relationship associations and disassociations
- 🧪 **Dry Run Mode** - Preview all changes without writing to the target environment
- 📊 **Excel Reporting** - Summary and error report of all migration actions

### Process Management
- ⚙️ **Process State Control** - Activate/deactivate workflows, cloud flows, business rules, actions, business process flows, and duplicate detection rules
- 🎯 **Pattern-based Rules** - Use wildcard patterns to define which processes should be inactive
- 🔄 **CI/CD Ready** - Run post-deployment to ensure processes are in the correct state
- 🧪 **Dry Run Mode** - Preview changes without modifying any process states
- 📊 **Excel Reporting** - Summary and detailed Excel report of all actions taken

### Data Patch
- 🩹 **Targeted Record Updates** - Apply field-level updates to specific Dataverse records by key lookup
- 📁 **Config File or Inline JSON** - Supply config from a file (`--config`) or as a JSON blob (`--config-json`) for pipeline use
- 🔐 **Pipeline-friendly** - Designed for Key Vault integration: retrieve a JSON secret and pipe it directly to the command
- 🔍 **Skip-if-unchanged** - Reads the current value first; skips the update if it is already correct
- 📊 **Excel Reporting** - Per-patch outcome report: Updated / Unchanged / Not Found / Error

### Solution Layer Analysis
- 🔍 **Unmanaged Layer Detection** - Identifies solution components where an unmanaged layer sits above the managed solution layer, preventing deployment changes from taking effect
- 📊 **Excel Report** - Summary and detail sheets listing affected components by type and name with full layer stack
- 🚀 **Post-deployment Use** - Run after a solution import to confirm changes will take effect

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
      "excludeFields": ["rob_temporaryfield"],
      "includeFields": []
    },
    {
      "logicalName": "rob_item",
      "includeFields": ["rob_name", "rob_categoryid", "rob_priorityid"]
    }
  ],
  "relationships": [
    {
      "relationshipName": "rob_category_priority",
      "displayName": "Category to Priority",
      "entity1NameField": "rob_name",
      "entity2NameField": "rob_priorityname"
    }
  ]
}
```

> **Tip:** The `relationships` config is compatible with `refdata-migrate` — you can use the same JSON file for both tools. See [Shared Config](#shared-config) below.

**Behaviour**:
- `includeFields` restricts comparison to only the specified fields (acts as an allowlist per table)
- `excludeFields` removes specific fields from comparison
- Relationship details are resolved automatically via metadata lookup — only `relationshipName` is required
- `displayName`, `entity1NameField`, `entity2NameField` are optional overrides for report display

**Output**: Excel workbook with:
- Summary sheet showing all tables and relationship difference counts
- Detail sheets for each table with differences (NEW/MODIFIED/DELETED records)
- Detail sheets for each N:N relationship with differences (NEW/DELETED associations)
- Field-level comparison using formatted values (human-readable lookups and option sets)
- GUIDs resolved to display names for relationship associations

### Reference Data Migration

Migrate reference data from a source Dataverse environment to a target environment.

#### Basic Usage

```bash
powerapps-cli refdata-migrate \
  --config refdata-migrate-config.json \
  --source-url "https://dev.crm.dynamics.com" \
  --target-url "https://test.crm.dynamics.com" \
  --client-id "$CLIENT_ID" \
  --client-secret "$CLIENT_SECRET" \
  --output migration-report.xlsx
```

#### Dry Run (Preview Changes)

```bash
powerapps-cli refdata-migrate \
  --config refdata-migrate-config.json \
  --source-url "https://dev.crm.dynamics.com" \
  --target-url "https://test.crm.dynamics.com" \
  --client-id "$CLIENT_ID" \
  --client-secret "$CLIENT_SECRET" \
  --dry-run \
  --output migration-preview.xlsx
```

#### Using Connection Strings

```bash
powerapps-cli refdata-migrate \
  --config refdata-migrate-config.json \
  --source-connection "$DEV_CONNECTION_STRING" \
  --target-connection "$TEST_CONNECTION_STRING" \
  --output migration-report.xlsx
```

#### Force Full Sync

```bash
powerapps-cli refdata-migrate \
  --config refdata-migrate-config.json \
  --source-connection "$DEV_CONNECTION_STRING" \
  --target-connection "$TEST_CONNECTION_STRING" \
  --force \
  --output migration-report.xlsx
```

#### Example Config File

```json
{
  "batchSize": 1000,
  "tables": [
    {
      "logicalName": "rob_category",
      "manageState": true
    },
    {
      "logicalName": "rob_priority",
      "filter": "<filter><condition attribute='statecode' operator='eq' value='0'/></filter>",
      "excludeFields": ["rob_legacycode"],
      "manageState": false
    },
    {
      "logicalName": "rob_item",
      "includeFields": ["rob_name", "rob_categoryid", "rob_priorityid"]
    }
  ],
  "relationships": [
    {
      "relationshipName": "rob_category_priority"
    }
  ]
}
```

**Behaviour**:
- By default, only records that differ from the target are migrated (diff mode)
- Use `--force` to push all records regardless of whether they have changed
- Lookups are applied in a second pass to handle self-referential relationships
- `manageState: true` will sync the active/inactive state of records
- `includeFields` restricts migration to only the specified fields (system fields always excluded)
- `excludeFields` removes specific fields from migration
- Relationship details are resolved automatically via metadata lookup — only `relationshipName` is required

**Output**: Excel report with:
- Summary sheet showing environment info, totals, and per-table results
- Errors sheet (if any failures occurred) with table, record ID, phase, and error message

### Shared Config

`refdata-compare` and `refdata-migrate` share the same config schema. A single JSON file can drive both tools — each tool reads what it needs and silently ignores the rest.

```json
{
  "batchSize": 1000,
  "excludeSystemFields": true,
  "tables": [
    {
      "logicalName": "rob_category",
      "primaryIdField": "rob_categoryid",
      "primaryNameField": "rob_name",
      "filter": "<filter><condition attribute='statecode' operator='eq' value='0'/></filter>",
      "excludeFields": [],
      "includeFields": [],
      "manageState": true
    }
  ],
  "relationships": [
    {
      "relationshipName": "rob_category_priority",
      "displayName": "Category to Priority",
      "entity1NameField": "rob_name",
      "entity2NameField": "rob_priorityname"
    }
  ]
}
```

| Property | Compare | Migrate |
|---|---|---|
| `tables[].logicalName` | ✓ | ✓ |
| `tables[].filter` | ✓ | ✓ |
| `tables[].excludeFields` | ✓ | ✓ |
| `tables[].includeFields` | ✓ (allowlist) | ✓ (allowlist) |
| `tables[].manageState` | ignored | ✓ |
| `tables[].primaryIdField` / `primaryNameField` / `displayName` | ✓ | ignored |
| `relationships[].relationshipName` | ✓ (metadata lookup) | ✓ (metadata lookup) |
| `relationships[].displayName` | ✓ (report tab name) | ✓ (report tab name) |
| `relationships[].entity1NameField` / `entity2NameField` | ✓ (display names) | ignored |
| `batchSize` | ignored | ✓ |
| `excludeSystemFields` / `globalExcludeFields` | ✓ | ignored |

### Process Management

Manage Dataverse process states (workflows, cloud flows, business rules, actions) to ensure correct activation/deactivation post-deployment.

#### Basic Usage

```bash
powerapps-cli process-manage \
  --config process-config.json \
  --url "https://prod.crm.dynamics.com" \
  --client-id "$CLIENT_ID" \
  --client-secret "$CLIENT_SECRET" \
  --output process-report.xlsx
```

#### Dry Run (Preview Changes)

```bash
powerapps-cli process-manage \
  --config process-config.json \
  --url "https://prod.crm.dynamics.com" \
  --client-id "$CLIENT_ID" \
  --client-secret "$CLIENT_SECRET" \
  --dry-run \
  --output process-preview.xlsx
```

#### Using Connection String

```bash
powerapps-cli process-manage \
  --config process-config.json \
  --connection-string "$PROD_CONNECTION_STRING" \
  --output process-report.xlsx
```

#### Example Config File

```json
{
  "solutions": ["Solution1", "Solution2"],
  "inactivePatterns": [
    "ZZ*",
    "Test - *",
    "Specific Process Name"
  ],
  "maxRetries": 3
}
```

**Behavior**:
- Processes matching `inactivePatterns` are **deactivated**
- All other processes are **activated**
- Retry logic handles parent-child dependencies
- Wildcards supported in patterns (* matches any characters)

**Output**: Excel report with:
- Summary showing total, activated, deactivated, unchanged, and failed processes
- Detailed list of all processes with name, type, expected state, actual state, and action taken

**Use Case**: Run in CI/CD pipelines after deployment to ensure processes are in the correct state.

### Data Patch

Apply targeted field-level updates to specific Dataverse records, driven by a config file or inline JSON blob. Designed for post-deployment pipelines where environment-specific values need patching (e.g. Power Pages authentication settings).

#### From a config file

```bash
powerapps-cli data-patch \
  --config patch.json \
  --connection-string "$SIT_CONNECTION_STRING" \
  --output data-patch-report.xlsx
```

#### From an inline JSON blob (e.g. from Key Vault in a pipeline)

```bash
# Azure DevOps / bash pipeline step
JSON=$(az keyvault secret show --name "data-patch-sit" --vault-name "myvault" --query "value" -o tsv)
powerapps-cli data-patch \
  --config-json "$JSON" \
  --connection-string "$SIT_CONNECTION_STRING" \
  --output data-patch-report.xlsx
```

#### Example Config File

```json
{
  "patches": [
    {
      "entity": "mspp_sitesetting",
      "keyField": "mspp_name",
      "key": "Authentication/OpenIdConnect/AzureADB2C/Authority",
      "valueField": "mspp_value",
      "value": "https://yourtenant.b2clogin.com/yourtenant.onmicrosoft.com/B2C_1A_Policy"
    },
    {
      "entity": "mspp_sitesetting",
      "keyField": "mspp_name",
      "key": "Authentication/OpenIdConnect/AzureADB2C/ClientId",
      "valueField": "mspp_value",
      "value": "00000000-0000-0000-0000-000000000000"
    }
  ]
}
```

**Behaviour**:
- Looks up each record by `keyField` / `key`; errors if not found or if multiple records match
- Reads the current `valueField` value and skips the update if it already matches
- `value` supports JSON strings, numbers, and booleans — type is inferred automatically
- Use `"type"` for fields that need explicit conversion: `"date"`, `"datetime"`, `"guid"`, `"optionset"` (wraps in `OptionSetValue`), `"lookup"` (value must be `{ "logicalName": "...", "id": "..." }`)
- `--config` and `--config-json` are mutually exclusive

**Output**: Excel report with one row per patch entry showing Entity, Key, Field, Old Value, New Value, and Status (Updated / Unchanged / Not Found / Ambiguous Match / Error).

**Pipeline pattern**:
```
1. Install solution
2. Retrieve JSON blob from Key Vault
3. powerapps-cli data-patch --config-json $json --connection-string $conn
```

One Key Vault secret per target environment, each containing the full JSON for that environment.

### Solution Layers

Check for unmanaged layers on a solution's components post-deployment. Unmanaged layers prevent managed solution changes from taking effect.

#### Basic Usage

```bash
powerapps-cli solution-layers \
  --solution "YourSolutionUniqueName" \
  --url "https://yourorg.crm.dynamics.com" \
  --client-id "$CLIENT_ID" \
  --client-secret "$CLIENT_SECRET"
```

#### With Connection String

```bash
powerapps-cli solution-layers \
  --solution "YourSolutionUniqueName" \
  --connection-string "$PROD_CONNECTION_STRING" \
  --output solution-layers.xlsx
```

**Behaviour**:
- Queries `msdyn_componentlayer` for all components belonging to the specified solution
- A component is flagged when the topmost layer is `Active` (the unmanaged customisations bucket)
- Reports all affected components with their type, name, and full layer stack

**Output**: Excel workbook with:
- Summary sheet showing solution name, environment, date, and count of affected components
- Unmanaged Layers sheet with Component Type, Component Name, Unmanaged Layer, and full layer stack (bottom to top)
- Clean result message if no unmanaged layers are found

**Pipeline pattern**:
```
1. Install managed solution
2. powerapps-cli solution-layers --solution $solutionName --connection-string $conn
3. Review report — any rows = someone has unmanaged customisations blocking your changes
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

### refdata-migrate

Migrates reference data from a source to a target Dataverse environment.

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
| `--dry-run` | Preview mode — no changes made to target | No | `false` |
| `--force` | Push all records regardless of whether they have changed | No | `false` |
| `-o, --output` | Output Excel report file path | No | `migration-report.xlsx` |
| `-v, --verbose` | Enable verbose output | No | `false` |

\* Either `--source-url`/`--target-url` or `--source-connection`/`--target-connection` must be provided.

### process-manage

Manages Dataverse process states (workflows, cloud flows, business rules, actions).

#### Options

| Option | Description | Required | Default |
|--------|-------------|----------|---------|
| `--config` | Path to JSON configuration file | Yes | - |
| `--url` | Environment URL | Yes* | - |
| `--connection-string` | Environment connection string | No | - |
| `--client-id` | Azure AD Application Client ID | No | - |
| `--client-secret` | Azure AD Application Client Secret | No | - |
| `--dry-run` | Preview changes without modifying states | No | `false` |
| `-o, --output` | Output Excel report file path | No | `process-report.xlsx` |
| `-v, --verbose` | Enable verbose output | No | `false` |

\* Either `--url` or `--connection-string` must be provided.

### solution-layers

Reports unmanaged layers on a solution's components post-deployment.

#### Options

| Option | Description | Required | Default |
|--------|-------------|----------|---------|
| `-s, --solution` | Unique name of the solution to inspect | Yes | - |
| `--url, -u` | Environment URL | Yes* | - |
| `--connection-string` | Environment connection string | No | - |
| `--client-id` | Azure AD Application Client ID | No | - |
| `--client-secret` | Azure AD Application Client Secret | No | - |
| `-o, --output` | Output Excel report file path | No | `solution-layers.xlsx` |
| `-v, --verbose` | Enable verbose output | No | `false` |

\* Either `--url` or `--connection-string` must be provided.

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
  ├── ConstantsCommand.cs       # Constants generation CLI command
  ├── RefDataCompareCommand.cs  # Reference data comparison CLI command
  ├── RefDataMigrateCommand.cs  # Reference data migration CLI command
  ├── ProcessManageCommand.cs   # Process management CLI command
  ├── DataPatchCommand.cs       # Data patch CLI command
  └── SolutionLayersCommand.cs  # Solution layer analysis CLI command
Services/
  ├── SchemaService.cs          # Schema export orchestration
  ├── SchemaExtractor.cs        # Metadata extraction with solution filtering
  ├── SchemaExporter.cs         # Export to JSON/XLSX formats
  ├── ConstantsGenerator.cs     # Constants generation orchestration
  ├── CodeTemplateGenerator.cs  # C# code template generation
  ├── ConstantsFilter.cs        # Entity/attribute filtering logic
  ├── IdentifierFormatter.cs    # C# identifier formatting (PascalCase, sanitization)
  ├── MetadataMapper.cs         # SDK to model mapping
  ├── IRecordComparer.cs        # Record comparison interface
  ├── RecordComparer.cs         # Record and association comparison logic
  ├── IComparisonReporter.cs    # Comparison report interface
  ├── ComparisonReporter.cs     # Comparison report Excel generation
  ├── IRefDataMigrator.cs       # Reference data migrator interface
  ├── RefDataMigrator.cs        # Reference data migration logic
  ├── IMigrationReporter.cs     # Migration reporter interface
  ├── MigrationReporter.cs      # Migration report Excel generation
  ├── IProcessManager.cs        # Process management interface
  ├── ProcessManager.cs         # Process state management logic
  ├── ProcessReporter.cs        # Process report Excel generation
  ├── IDataPatchReporter.cs     # Data patch reporter interface
  ├── DataPatchReporter.cs      # Data patch report Excel generation
  ├── ISolutionLayerService.cs  # Solution layer service interface
  ├── SolutionLayerService.cs   # Solution layer querying and unmanaged layer detection
  ├── ISolutionLayerReporter.cs # Solution layer reporter interface
  └── SolutionLayerReporter.cs  # Solution layer report Excel generation
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
  ├── ConstantsOutputConfig.cs  # Constants output settings
  ├── RefDataCompareConfig.cs   # Reference data comparison configuration
  ├── ComparisonResult.cs       # Table comparison result models
  ├── RelationshipComparisonResult.cs # N:N relationship comparison models
  ├── RefDataMigrateConfig.cs   # Reference data migration configuration
  ├── RefDataMigrateModels.cs   # Migration result models
  ├── ProcessManageConfig.cs    # Process management configuration
  ├── ProcessManageModels.cs    # Process state models
  ├── DataPatchConfig.cs        # Data patch configuration
  ├── DataPatchModels.cs        # Data patch result models
  └── SolutionLayerResult.cs    # Solution layer analysis result models
```

## Testing

The project includes unit tests covering schema extraction, constants generation, reference data comparison, and process management.

### Run Tests

```bash
dotnet test
```

### Run Tests with Coverage

```bash
# Using test-scripts helper
.\tests\scripts\run-coverage.ps1

# Or manually
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
reportgenerator -reports:"tests/PowerApps.CLI.Tests/TestResults/coverage.cobertura.xml" \
  -targetdir:"TestResults/CoverageReport" -reporttypes:"Html;TextSummary"
```

Current test coverage:
- **330+ passing tests** (100% pass rate)
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
- ✅ Command orchestration (all 7 commands)
- ✅ Reference data comparison (table records, N:N relationships, name resolution)
- ✅ Reference data migration (all 4 passes, diff/force modes, column filtering, N:N sync)
- ✅ Process management (pattern matching, retry logic, state determination)
- ✅ Data patch (lookup, skip-if-unchanged, update, error handling)
- ✅ Solution layer analysis (unmanaged layer detection, component type mapping, clean result handling)

## Development

### Project Structure

- `src/PowerApps.CLI/` - Main application code
  - `Commands/` - CLI command definitions
  - `Services/` - Business logic and orchestration
  - `Infrastructure/` - External integrations and utilities
  - `Models/` - Data models and schemas
- `tests/PowerApps.CLI.Tests/` - Unit tests
- `tests/scripts/` - Local test scripts with sample usage (credentials not committed)

### Dependencies

- **Microsoft.PowerPlatform.Dataverse.Client** - Dataverse SDK
- **ClosedXML** - Excel file generation
- **System.CommandLine** - CLI framework
- **xUnit** - Testing framework
- **Moq** - Mocking library

## Contributing

Contributions are welcome! Please ensure:
- All tests pass
- New features include unit tests
- Code follows existing patterns and conventions

## License

MIT License - see [LICENSE](LICENSE) file for details.

This project is provided as-is with no warranties. Feel free to use, modify, and distribute as needed.
