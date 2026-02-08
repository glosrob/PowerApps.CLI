# Test Scripts

This directory contains test scripts for development and testing purposes.

**IMPORTANT**: Test scripts with credentials are git-ignored to prevent accidental commit.

## Structure

```
tests/scripts/
├── connections.sample.json   # Template for credentials (committed)
├── connections.json          # Your credentials (git-ignored)
├── run-coverage.ps1          # Run tests with code coverage
├── run-tests.ps1             # Run all unit tests
├── schema-export/            # schema-export command scripts
├── constants-generate/       # constants-generate command scripts
│   └── sample-config.json    # Example config (committed)
├── refdata-compare/          # refdata-compare command scripts
│   └── sample-config.json    # Example config (committed)
└── process-manage/           # process-manage command scripts
    └── sample-config.json    # Example config (committed)
```

## Setup

1. Copy `connections.sample.json` to `connections.json`
2. Fill in your Dataverse environment URLs, client IDs, and client secrets
3. `connections.json` is git-ignored and will not be committed

## Safe Scripts (Committed)

- `connections.sample.json` - Template for credentials
- `run-coverage.ps1` - Run tests with code coverage and HTML reports
- `run-tests.ps1` - Run all unit tests
- `*/sample-config.json` - Example configuration files per command

## Credential Test Scripts (Git-Ignored)

Each command subfolder contains test scripts for different authentication methods.
All scripts load credentials from `connections.json`:
- `test-connection-string.ps1` - Test with connection string authentication
- `test-env-vars.ps1` - Test with environment variables
- `test-interactive.ps1` - Test with interactive browser authentication
- `test-service-principal.ps1` - Test with client ID/secret authentication

## Usage

### Running Unit Tests

```powershell
.\tests\scripts\run-tests.ps1
```

### Running with Coverage

```powershell
.\tests\scripts\run-coverage.ps1
```

### Testing with Real Dataverse

1. Complete the setup steps above (create `connections.json`)
2. Run a test script from the repository root, e.g.:
   ```powershell
   .\tests\scripts\refdata-compare\test-connection-string.ps1
   ```

**Never commit `connections.json` or credential test files!**
