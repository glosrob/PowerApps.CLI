# Tests

## Unit Tests

Unit tests live in `tests/PowerApps.CLI.Tests/` and use xUnit + Moq. They run against no real Dataverse connection — everything is mocked.

```powershell
# Run all unit tests
.\tests\scripts\run-tests.ps1

# Run with code coverage and HTML report
.\tests\scripts\run-coverage.ps1
```

Coverage reports are generated to `tests/coverage/report/index.html`.

---

## Test Scripts

`tests/scripts/` contains helper scripts for manual testing against real Dataverse environments.

### Structure

```
tests/scripts/
├── connections.sample.json          # Template for credentials (committed)
├── connections.json                 # Your credentials (git-ignored)
├── generate-integration-schema.ps1  # Re-export integration test solution schema
├── run-coverage.ps1                 # Run unit tests with code coverage
├── run-tests.ps1                    # Run all unit tests
└── manual/                          # Per-command fixtures and ad-hoc test scripts
    ├── constants-generate/
    │   └── test.ps1                 # Validates --skip-virtual-fields behaviour
    ├── data-patch/
    │   └── sample-config.json
    ├── process-manage/
    │   └── sample-config.json
    ├── refdata-compare/
    │   └── sample-config.json
    ├── refdata-migrate/
    │   └── sample-config.json
    └── solution-layers/
```

### Setup

1. Copy `connections.sample.json` to `connections.json`
2. Fill in your Dataverse environment details under `Default` and `IntegrationTests`
3. `connections.json` is git-ignored and will never be committed

### connections.json structure

```json
{
  "Default": {
    "Url": "https://your-environment.crm11.dynamics.com/",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "Solution": "your_solution_unique_name"
  },
  "IntegrationTests": {
    "Url": "https://your-environment.crm11.dynamics.com/",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "SolutionName": "XRTSoftIntegrationTests",
    "PrimaryTable": "xrt_integrationtest",
    "SecondaryTable": "xrt_integrationothertest"
  }
}
```

**Never commit `connections.json` or any script containing real credentials.**

---

## Integration Tests

Integration tests require a real Dataverse environment with specific solution artefacts in place. They are opt-in — all tests are tagged `[Trait("Category", "Integration")]` and excluded from the default test run.

```powershell
# Run integration tests only
dotnet test --filter "Category=Integration"
```

### Environment Variables

Alternatively, set these before running:

| Variable | Description |
|---|---|
| `DATAVERSE_URL` | Environment URL, e.g. `https://yourorg.crm11.dynamics.com` |
| `DATAVERSE_CLIENT_ID` | App registration client ID |
| `DATAVERSE_CLIENT_SECRET` | App registration client secret |

---

### Test Solution

**Unique name:** `XRTSoftIntegrationTests`  
**Publisher prefix:** `xrt_`

The solution must exist in the target environment before running any integration tests. The committed file `tests/fixtures/integration-test-schema.json` is the canonical reference for its contents — regenerate it with:

```powershell
.\tests\scripts\generate-integration-schema.ps1
```

---

### Tables

#### `xrt_integrationtest` — Primary test table

The main workhorse. Contains columns of every supported type.

| Logical Name | Type | Notes |
|---|---|---|
| `xrt_name` | Single-line text | Primary name / stable identifier |
| `xrt_multilinetext` | Multiline text | |
| `xrt_wholenumber` | Whole number | |
| `xrt_decimalnumber` | Decimal | |
| `xrt_floatnumber` | Float | |
| `xrt_currencyfield` | Currency | |
| `xrt_currencyfield_base` | Currency (base) | Auto-generated companion field |
| `xrt_boolfield` | Yes/No | |
| `xrt_localchoice` | Local choice | |
| `xrt_globalchoice` | Global choice | References `xrt_globalchoice` |
| `xrt_multiselectglobalchoicefield` | Multi-select global choice | References `xrt_globalchoice` |
| `xrt_dateonlyfield` | Date only | |
| `xrt_datetimefield` | Date and time | |
| `xrt_lookupfield` | Lookup | References `xrt_integrationothertest` |
| `xrt_customerfield` | Customer | Polymorphic lookup (account or contact) |
| `xrt_filefield` | File | |
| `xrt_imagefield` | Image | |
| `xrt_formulafield` | Formula | |

#### `xrt_integrationothertest` — Lookup target table

Simple table used as the target for `xrt_lookupfield`. No custom columns beyond the default name field. Must contain at least one record for lookup resolution tests to work.

---

### Global Choice

**Logical name:** `xrt_globalchoice`

| Label | Value |
|---|---|
| Choice 1 | 971940000 |
| Choice 2 | 971940001 |

---

### Relationships

| Type | From | To | Notes |
|---|---|---|---|
| Lookup (N:1) | `xrt_integrationtest` | `xrt_integrationothertest` | Via `xrt_lookupfield` |
| Many-to-many | `xrt_integrationtest` | `xrt_integrationothertest` | `xrt_IntegrationTest_xrt_IntegrationOtherTest_xrt_IntegrationOtherTest` |

---

### Test Records

`xrt_integrationtest` should contain at least 3 records with distinct `xrt_name` values (e.g. `record-001`, `record-002`, `record-003`). At least one should have a populated `xrt_lookupfield` and at least one N:N association to `xrt_integrationothertest`.

---

### Processes (for `process-manage` tests)

All processes below should be **deactivated** in the test solution by default.

| Name | Type | Notes |
|---|---|---|
| *(sync workflow name)* | Synchronous workflow | On `xrt_integrationtest` |
| *(async workflow name)* | Asynchronous workflow | On `xrt_integrationtest`; calls the child workflow below |
| *(child workflow name)* | Child workflow | Dependency on async workflow — exercises multi-pass activation ordering |
| *(action name)* | Action | |
| *(cloud flow name)* | Cloud flow | |
| *(child flow name)* | Child flow | |

> Fill in the actual process names once confirmed.

---

### Plugin Assembly (for `process-manage` tests)

**Assembly:** `XRT.IntegrationTestPluginLib`  
**Source:** `tests/XRT.IntegrationTestPluginLib/`  
**Class:** `XRT.IntegrationTestPluginLib.ExamplePlugin`

The assembly must be registered in the environment via the Plugin Registration Tool. Two plugin steps must be added to the test solution and left **disabled** by default.

> No-op stub used only to exercise the plugin step activation path in `process-manage`. No production use.
