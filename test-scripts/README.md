# Test Scripts

This directory contains test scripts for development and testing purposes.

**⚠️ IMPORTANT**: Test scripts with credentials are git-ignored to prevent accidental commit.

## Safe Scripts (Committed)

- `run-coverage.ps1` - Run tests with code coverage and HTML reports
- `run-tests.ps1` - Run all unit tests
- `test-constants-command.ps1` - Test/demonstrate the constants-generate command (no credentials required)
- `sample-constants-config.json` - Example configuration file for constants generation

## Credential Test Scripts (Git-Ignored)

- `test-interactive.ps1` - Test with interactive browser authentication
- `test-service-principal.ps1` - Test with client ID/secret authentication
- `test-connection-string.ps1` - Test with connection string authentication

## Usage

### Running Unit Tests

```powershell
.\test-scripts\run-tests.ps1
```

### Testing Constants Command

```powershell
# Show help
.\test-scripts\test-constants-command.ps1 -ShowHelp

# Show examples (dry run)
.\test-scripts\test-constants-command.ps1 -DryRun

# Create sample config
.\test-scripts\test-constants-command.ps1
```

### Testing with Real Dataverse

1. Copy a credential test script (e.g., `test-connection-string.ps1`)
2. Update with your environment details
3. Run the script

**Never commit credential test files!**
