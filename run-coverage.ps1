#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Runs tests with code coverage and generates an HTML report.
.DESCRIPTION
    This script runs the test suite with Coverlet code coverage collection,
    generates an HTML report using ReportGenerator, and opens it in the browser.
#>

param(
    [switch]$NoBrowser,
    [string]$Configuration = "Debug"
)

Write-Host "Running tests with code coverage..." -ForegroundColor Cyan

# Clean previous coverage results
$coverageDir = ".\coverage"
if (Test-Path $coverageDir) {
    Remove-Item $coverageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $coverageDir -Force | Out-Null

# Run tests with coverage
dotnet test --configuration $Configuration `
    --collect:"XPlat Code Coverage" `
    --results-directory $coverageDir `
    --settings coverlet.runsettings `
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

if ($LASTEXITCODE -ne 0) {
    Write-Host "Tests failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Find the coverage file
$coverageFile = Get-ChildItem -Path $coverageDir -Filter "coverage.opencover.xml" -Recurse | Select-Object -First 1

if (-not $coverageFile) {
    Write-Host "Coverage file not found!" -ForegroundColor Red
    exit 1
}

Write-Host "Generating HTML report..." -ForegroundColor Cyan

# Install ReportGenerator if not already installed
$reportGenerator = dotnet tool list -g | Select-String "dotnet-reportgenerator-globaltool"
if (-not $reportGenerator) {
    Write-Host "Installing ReportGenerator..." -ForegroundColor Yellow
    dotnet tool install -g dotnet-reportgenerator-globaltool
}

# Generate HTML report
$reportDir = Join-Path $coverageDir "report"
reportgenerator `
    -reports:$coverageFile.FullName `
    -targetdir:$reportDir `
    -reporttypes:"Html;Badges;TextSummary" `
    -verbosity:Warning

if ($LASTEXITCODE -ne 0) {
    Write-Host "Report generation failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Display summary
$summaryFile = Join-Path $reportDir "Summary.txt"
if (Test-Path $summaryFile) {
    Write-Host "`nCoverage Summary:" -ForegroundColor Green
    Get-Content $summaryFile | Write-Host
}

# Open report in browser
if (-not $NoBrowser) {
    $indexFile = Join-Path $reportDir "index.html"
    Write-Host "`nOpening coverage report in browser..." -ForegroundColor Cyan
    Start-Process $indexFile
}
else {
    Write-Host "`nReport generated at: $reportDir\index.html" -ForegroundColor Green
}
