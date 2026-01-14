#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Builds the PowerApps CLI project.
.DESCRIPTION
    This script builds the solution in the specified configuration.
.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Debug.
.PARAMETER Clean
    Clean before building.
.PARAMETER Restore
    Restore packages before building.
.EXAMPLE
    .\build.ps1
    .\build.ps1 -Configuration Release
    .\build.ps1 -Clean -Restore
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$Clean,
    [switch]$Restore
)

Write-Host "PowerApps CLI - Build" -ForegroundColor Cyan
Write-Host "====================`n" -ForegroundColor Cyan

# Clean
if ($Clean) {
    Write-Host "Cleaning..." -ForegroundColor Yellow
    dotnet clean --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Clean failed!" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# Restore
if ($Restore) {
    Write-Host "Restoring packages..." -ForegroundColor Yellow
    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Restore failed!" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# Build
Write-Host "Building ($Configuration)..." -ForegroundColor Green
dotnet build --configuration $Configuration --no-restore

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✓ Build succeeded!" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "`n✗ Build failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}
