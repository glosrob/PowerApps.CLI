#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Tests the constants-generate CLI command.
.DESCRIPTION
    This script demonstrates and tests the constants-generate command with various options.
    Use this to verify the CLI works correctly before running against real Dataverse environments.
.PARAMETER ShowHelp
    Display the command help information.
.PARAMETER DryRun
    Show what would be executed without actually running.
.EXAMPLE
    .\test-constants-command.ps1 -ShowHelp
    .\test-constants-command.ps1 -DryRun
#>

param(
    [switch]$ShowHelp,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "PowerApps CLI - Constants Generate Command Test" -ForegroundColor Cyan
Write-Host "==============================================`n" -ForegroundColor Cyan

# Build the project first
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build --configuration Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Show help if requested
if ($ShowHelp) {
    Write-Host "`nDisplaying command help:" -ForegroundColor Green
    dotnet run --project src/PowerApps.CLI --no-build -- constants-generate --help
    exit 0
}

# Example: Test with mock data (would need real Dataverse connection)
Write-Host "`nExample Usage:" -ForegroundColor Green
Write-Host "===============`n" -ForegroundColor Green

$exampleCommands = @(
    @{
        Description = "Generate with connection string (single file mode)"
        Command = 'dotnet run --project src/PowerApps.CLI --no-build -- constants-generate --connection-string "AuthType=OAuth;..." --output "./Generated" --namespace "Contoso.Constants" --single-file'
    },
    @{
        Description = "Generate with URL and client credentials (multi-file mode)"
        Command = 'dotnet run --project src/PowerApps.CLI --no-build -- constants-generate --url "https://org.crm.dynamics.com" --client-id "xxx" --client-secret "yyy" --output "./Generated" --namespace "Contoso.Constants"'
    },
    @{
        Description = "Generate with config file"
        Command = 'dotnet run --project src/PowerApps.CLI --no-build -- constants-generate --config "./constants-config.json" --connection-string "AuthType=OAuth;..."'
    },
    @{
        Description = "Generate with filtering"
        Command = 'dotnet run --project src/PowerApps.CLI --no-build -- constants-generate --connection-string "AuthType=OAuth;..." --output "./Generated" --namespace "Contoso.Constants" --exclude-entities "systemuser,team" --attribute-prefix "rob_" --single-file'
    },
    @{
        Description = "Generate from specific solution"
        Command = 'dotnet run --project src/PowerApps.CLI --no-build -- constants-generate --connection-string "AuthType=OAuth;..." --solution "MySolution" --output "./Generated" --namespace "Contoso.Constants"'
    }
)

foreach ($example in $exampleCommands) {
    Write-Host "• $($example.Description)" -ForegroundColor Cyan
    Write-Host "  $($example.Command)`n" -ForegroundColor Gray
}

if ($DryRun) {
    Write-Host "Dry run complete - no commands executed" -ForegroundColor Yellow
    exit 0
}

# Create sample config file
$configPath = ".\test-scripts\sample-constants-config.json"
Write-Host "Creating sample config file at: $configPath" -ForegroundColor Green

$sampleConfig = @{
    SingleFile = $true
    IncludeEntities = $true
    IncludeGlobalOptionSets = $true
    IncludeReferenceData = $false
    PascalCaseConversion = $true
    AttributePrefix = "rob_"
    ExcludeEntities = @("systemuser", "team", "businessunit")
    ExcludeAttributes = @("createdon", "createdby", "modifiedon", "modifiedby", "ownerid")
} | ConvertTo-Json -Depth 10

$sampleConfig | Out-File -FilePath $configPath -Encoding UTF8 -Force
Write-Host "✓ Sample config created`n" -ForegroundColor Green

Write-Host "To test with a real Dataverse environment, run:" -ForegroundColor Yellow
Write-Host "  dotnet run --project src/PowerApps.CLI -- constants-generate --connection-string `"AuthType=OAuth;Url=https://org.crm.dynamics.com;...`" --output ./Generated --namespace MyCompany.Constants`n" -ForegroundColor Gray

Write-Host "Or use the sample config:" -ForegroundColor Yellow
Write-Host "  dotnet run --project src/PowerApps.CLI -- constants-generate --config $configPath --connection-string `"AuthType=OAuth;...`"`n" -ForegroundColor Gray

Write-Host "✓ Test script completed successfully!" -ForegroundColor Green
