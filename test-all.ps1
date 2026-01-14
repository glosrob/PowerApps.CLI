#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Builds and runs all tests for the PowerApps CLI project.
.DESCRIPTION
    This script performs a clean build and runs the complete test suite.
    Use this as the main test script during development.
.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Debug.
.EXAMPLE
    .\test-all.ps1
    .\test-all.ps1 -Configuration Release
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

Write-Host "PowerApps CLI - Build & Test" -ForegroundColor Cyan
Write-Host "============================`n" -ForegroundColor Cyan

# Build
Write-Host "Building ($Configuration)..." -ForegroundColor Green
dotnet build --configuration $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n✗ Build failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "✓ Build succeeded`n" -ForegroundColor Green

# Test
Write-Host "Running tests..." -ForegroundColor Green
dotnet test --configuration $Configuration --no-build

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✓ All tests passed!" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "`n✗ Tests failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}
