#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Validates that --skip-virtual-fields excludes virtual attributes from generated constants.
.DESCRIPTION
    Builds the CLI, then runs constants-generate twice against the configured environment:
      1. Without --skip-virtual-fields (baseline)
      2. With --skip-virtual-fields
    Confirms that known virtual fields (e.g. createdbyname, owneridname) appear in the
    baseline output but are absent when the flag is set.
.EXAMPLE
    .\test.ps1
#>

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Resolve paths
# ---------------------------------------------------------------------------
$ScriptDir       = $PSScriptRoot
$RepoRoot        = Resolve-Path (Join-Path $ScriptDir "../../..")
$ConnectionsFile = Join-Path $ScriptDir "../connections.json"
$ProjectPath     = Join-Path $RepoRoot "src/PowerApps.CLI/PowerApps.CLI.csproj"
$OutputBase      = Join-Path $ScriptDir "output"
$OutputBaseline  = Join-Path $OutputBase "baseline"
$OutputSkipped   = Join-Path $OutputBase "skip-virtual"

# ---------------------------------------------------------------------------
# Load connection settings
# ---------------------------------------------------------------------------
if (-not (Test-Path $ConnectionsFile)) {
    Write-Error "connections.json not found at: $ConnectionsFile"
    exit 1
}

$conn         = Get-Content $ConnectionsFile -Raw | ConvertFrom-Json
$url          = $conn.Url
$clientId     = $conn.ClientId
$clientSecret = $conn.ClientSecret
$solution     = $conn.Solution

if (-not $url) { Write-Error "connections.json must contain a 'Url' value."; exit 1 }

# Build connection string if client credentials supplied
$connectionString = $null
if ($clientId -and $clientSecret) {
    $connectionString = "AuthType=ClientSecret;Url=$url;ClientId=$clientId;ClientSecret=$clientSecret"
}

# ---------------------------------------------------------------------------
# Build the CLI
# ---------------------------------------------------------------------------
Write-Host "`nBuilding CLI..." -ForegroundColor Cyan
dotnet build $ProjectPath -c Debug 2>&1 | Where-Object { $_ -match "error|warning|succeeded|failed" } | Write-Host
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed."; exit 1 }

$CliExe = Join-Path $RepoRoot "src/PowerApps.CLI/bin/Debug/net8.0/powerapps-cli.exe"
if (-not (Test-Path $CliExe)) {
    Write-Error "CLI executable not found at: $CliExe"
    exit 1
}

# ---------------------------------------------------------------------------
# Helper: run constants-generate
# ---------------------------------------------------------------------------
function Invoke-ConstantsGenerate {
    param(
        [string]$OutputPath,
        [string[]]$ExtraArgs
    )

    if (Test-Path $OutputPath) { Remove-Item $OutputPath -Recurse -Force }
    New-Item -ItemType Directory -Path $OutputPath | Out-Null

    $cliArgs = @(
        "constants-generate",
        "--output", $OutputPath,
        "--namespace", "Test.Constants",
        "--single-file",
        "--exclude-entities", "systemuser,team,businessunit",
        "--exclude-attributes", "createdon,createdby,modifiedon,modifiedby,ownerid"
    )

    if ($connectionString) {
        $cliArgs += "--connection-string", $connectionString
    } else {
        $cliArgs += "--url", $url
    }

    if ($solution) {
        $cliArgs += "--solution", $solution
    }

    $cliArgs += $ExtraArgs

    & $CliExe @cliArgs | Out-Host
    return $LASTEXITCODE
}

# ---------------------------------------------------------------------------
# Run 1: baseline (virtual fields included)
# ---------------------------------------------------------------------------
Write-Host "`n--- Run 1: baseline (no --skip-virtual-fields) ---" -ForegroundColor Yellow
$exitCode = Invoke-ConstantsGenerate -OutputPath $OutputBaseline -ExtraArgs @()
if ($exitCode -ne 0) { Write-Error "Baseline run failed (exit $exitCode)."; exit 1 }

# ---------------------------------------------------------------------------
# Run 2: with --skip-virtual-fields
# ---------------------------------------------------------------------------
Write-Host "`n--- Run 2: with --skip-virtual-fields ---" -ForegroundColor Yellow
$exitCode = Invoke-ConstantsGenerate -OutputPath $OutputSkipped -ExtraArgs @("--skip-virtual-fields")
if ($exitCode -ne 0) { Write-Error "Skip-virtual run failed (exit $exitCode)."; exit 1 }

# ---------------------------------------------------------------------------
# Validate
# ---------------------------------------------------------------------------
Write-Host "`n--- Validating output ---" -ForegroundColor Cyan

# Known virtual fields present on every standard Dataverse entity
$virtualFields = @("createdbyname", "modifiedbyname", "owneridname", "owneridtype", "transactioncurrencyidname")

$baselineFiles = Get-ChildItem $OutputBaseline -Recurse -Filter "*.cs"
$skippedFiles  = Get-ChildItem $OutputSkipped  -Recurse -Filter "*.cs"

if ($baselineFiles.Count -eq 0) { Write-Error "No .cs files generated in baseline output."; exit 1 }
if ($skippedFiles.Count  -eq 0) { Write-Error "No .cs files generated in skip-virtual output."; exit 1 }

$baselineContent = $baselineFiles | Get-Content -Raw | Out-String
$skippedContent  = $skippedFiles  | Get-Content -Raw | Out-String

$passed = $true

foreach ($field in $virtualFields) {
    $inBaseline = $baselineContent -match [regex]::Escape("`"$field`"")
    $inSkipped  = $skippedContent  -match [regex]::Escape("`"$field`"")

    if ($inBaseline -and -not $inSkipped) {
        Write-Host "  PASS  '$field' present in baseline, absent when skipped" -ForegroundColor Green
    } elseif (-not $inBaseline) {
        Write-Host "  SKIP  '$field' not in baseline (entity may not be in this solution)" -ForegroundColor DarkGray
    } else {
        Write-Host "  FAIL  '$field' still present in skip-virtual output" -ForegroundColor Red
        $passed = $false
    }
}

# Sanity check: output with skip should be smaller (fewer constants)
$baselineSize = ($baselineFiles | Measure-Object -Property Length -Sum).Sum
$skippedSize  = ($skippedFiles  | Measure-Object -Property Length -Sum).Sum

if ($skippedSize -lt $baselineSize) {
    Write-Host "  PASS  Skip-virtual output is smaller ($skippedSize bytes) than baseline ($baselineSize bytes)" -ForegroundColor Green
}
if ($skippedSize -ge $baselineSize) {
    Write-Host "  WARN  Skip-virtual output is not smaller than baseline - virtual fields may not exist in this solution" -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
Write-Host ""
if ($passed) {
    Write-Host "All checks passed." -ForegroundColor Green
    exit 0
}
Write-Host "One or more checks failed." -ForegroundColor Red
exit 1
