#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Runs all unit tests for the PowerApps CLI project.
.DESCRIPTION
    Builds and runs the unit test suite with clear output. Integration tests
    (Category=Integration) are excluded by default. To run integration tests
    explicitly: dotnet test --filter "Category=Integration"
.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Debug.
.EXAMPLE
    .\run-tests.ps1
    .\run-tests.ps1 -Configuration Release
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

Write-Host "PowerApps CLI - Unit Tests" -ForegroundColor Cyan
Write-Host "==========================`n" -ForegroundColor Cyan

# Build and test
Write-Host "Building and running tests ($Configuration)...`n" -ForegroundColor Green
dotnet test --configuration $Configuration --filter "Category!=Integration"

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✓ All tests passed!" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "`n✗ Tests failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}
