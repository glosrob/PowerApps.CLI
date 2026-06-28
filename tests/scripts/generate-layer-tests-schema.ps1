#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Exports the solution-layers test solution schema to JSON.
.DESCRIPTION
    Runs schema-export against the XRTSoftLayerTests solution and writes the
    output to tests/fixtures/layer-tests-schema.json. Commit this file so the
    solution-layers integration tests have a stable reference of what the solution contains.
.EXAMPLE
    .\tests\scripts\generate-layer-tests-schema.ps1
#>

$ErrorActionPreference = "Stop"

$ScriptDir       = $PSScriptRoot
$RepoRoot        = Resolve-Path (Join-Path $ScriptDir "../..")
$ConnectionsFile = Join-Path $ScriptDir "connections.json"
$ProjectPath     = Join-Path $RepoRoot "src/PowerApps.CLI/PowerApps.CLI.csproj"
$OutputFile      = Join-Path $RepoRoot "tests/fixtures/layer-tests-schema.json"

if (-not (Test-Path $ConnectionsFile)) {
    Write-Error "connections.json not found at: $ConnectionsFile"
    exit 1
}

$conn         = Get-Content $ConnectionsFile -Raw | ConvertFrom-Json
$url          = $conn.LayerTests.Url
$clientId     = $conn.LayerTests.ClientId
$clientSecret = $conn.LayerTests.ClientSecret
$solution     = $conn.LayerTests.SolutionName

if (-not $url)      { Write-Error "connections.json must contain a 'LayerTests.Url' value."; exit 1 }
if (-not $solution) { Write-Error "connections.json must contain a 'LayerTests.SolutionName' value."; exit 1 }

Write-Host "Building CLI..." -ForegroundColor Cyan
dotnet build $ProjectPath -c Debug 2>&1 | Where-Object { $_ -match "error|warning|succeeded|failed" } | Write-Host
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed."; exit 1 }

$CliExe = Join-Path $RepoRoot "src/PowerApps.CLI/bin/Debug/net8.0/powerapps-cli.exe"
if (-not (Test-Path $CliExe)) {
    Write-Error "CLI executable not found at: $CliExe"
    exit 1
}

Write-Host "`nExporting schema for solution '$solution'..." -ForegroundColor Cyan

& $CliExe schema-export `
    --url $url `
    --client-id $clientId `
    --client-secret $clientSecret `
    --solution $solution `
    --output $OutputFile `
    --format json

if ($LASTEXITCODE -ne 0) {
    Write-Error "schema-export failed (exit $LASTEXITCODE)."
    exit 1
}

Write-Host "`nSchema written to: $OutputFile" -ForegroundColor Green
Write-Host "Commit this file to keep the solution-layers test reference up to date." -ForegroundColor Yellow
