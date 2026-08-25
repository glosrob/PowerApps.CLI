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
├── generate-integration-schema.ps1  # Re-export integration test solution schema (XRTSoftIntegrationTests)
├── generate-layer-tests-schema.ps1  # Re-export layer tests solution schema (XRTSoftLayerTests)
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
    └── schema-export/               # Git-ignored in full (see note below)
```

Per-command fixtures and scripts live under `manual/` only. There is no second copy at
the root of `tests/scripts/` - the root holds just the shared helpers listed above.

### What is committed and what is not

The `.gitignore` rules for this folder are scoped to `tests/scripts/manual/*`:

| Pattern | Effect |
|---|---|
| `sample-config.json` | Committed - the template other people copy |
| `*config.json` (any other name) | Ignored - assumed to hold real environment details |
| `test.ps1` | Committed - the shared, reusable script for a command |
| `test-*.ps1` | Ignored - your throwaway variants |
| `output/` | Ignored - anything a command writes during a manual run |

`manual/schema-export/` is ignored **in full**, so nothing in it can be committed - not
even `test.ps1`. This predates the `manual/` layout and is deliberate rather than
oversight, but it does make schema-export the one command whose manual script cannot be
shared.

> **Never commit a command's output report.** Reports carry the environment URL and real
> solution and component names from whichever environment produced them, and this
> repository is public. Default report filenames are git-ignored (`**/solution-layers*.xlsx`
> and friends); write manual-run output to a `manual/<command>/output/` folder, which is
> ignored wholesale.

### Setup

1. Copy `connections.sample.json` to `connections.json`
2. Fill in your Dataverse environment details under `Default`, `IntegrationTests`, and `LayerTests`
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
    "ClientSecret": "your-client-secret"
  },
  "LayerTests": {
    "Url": "https://your-environment.crm11.dynamics.com/",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "SolutionName": "XRTSoftLayerTests"
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

The solution must exist in the target environment before running any integration tests. The committed file [`tests/fixtures/integration-test-schema.json`](fixtures/integration-test-schema.json) is the canonical reference for its tables, columns, relationships, and choices — regenerate it with:

```powershell
.\tests\scripts\generate-integration-schema.ps1
```

### Solution-Layers Test Solution

**Unique name:** `XRTSoftLayerTests`
**Publisher prefix:** `xrt_`

A separate, minimal solution used exclusively by the `solution-layers` integration tests. It must exist in the target environment before running those tests. The committed file [`tests/fixtures/layer-tests-schema.json`](fixtures/layer-tests-schema.json) is the canonical reference for its components — regenerate it with:

```powershell
.\tests\scripts\generate-layer-tests-schema.ps1
```

---

### Plugin Assembly (for `process-manage` tests)

**Assembly:** `XRT.IntegrationTestPluginLib`
**Source:** `tests/XRT.IntegrationTestPluginLib/`
**Class:** `XRT.IntegrationTestPluginLib.ExamplePlugin`

The assembly must be registered in the environment via the Plugin Registration Tool. Two plugin steps must be added to the test solution and left **disabled** by default.

> No-op stub used only to exercise the plugin step activation path in `process-manage`. No production use.
