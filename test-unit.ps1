#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Runs all unit tests for the PowerApps CLI project.
.DESCRIPTION
    This script runs the complete unit test suite and displays results.
    Use this for quick test runs without coverage analysis.
.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Debug.
.PARAMETER Verbose
    Show detailed test output.
.EXAMPLE
    .\test-unit.ps1
    .\test-unit.ps1 -Configuration Release
    .\test-unit.ps1 -Verbose
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$Verbose
)

Write-Host "PowerApps CLI - Unit Tests" -ForegroundColor Cyan
Write-Host "=========================`n" -ForegroundColor Cyan

# Run tests
$testArgs = @(
    "test",
    "--configuration", $Configuration,
    "--no-build"
)

if ($Verbose) {
    $testArgs += "--verbosity", "detailed"
}

Write-Host "Running unit tests..." -ForegroundColor Green
dotnet @testArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✓ All tests passed!" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "`n✗ Tests failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}
