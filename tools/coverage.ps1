#!/usr/bin/env pwsh
# Runs every test project with code coverage and builds an HTML report with ReportGenerator.
#
# Requires the ReportGenerator global tool:
#   dotnet tool install -g dotnet-reportgenerator-globaltool
#
# Usage (from anywhere):
#   pwsh tools/coverage.ps1           # run tests, build the report, open it
#   pwsh tools/coverage.ps1 -NoOpen   # same, without opening a browser
#
# Output: coverage/report/index.html (the coverage/ folder is git-ignored).

[CmdletBinding()]
param(
    [switch]$NoOpen
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$coverageRoot = Join-Path $root 'coverage'
$rawDir = Join-Path $coverageRoot 'raw'
$reportDir = Join-Path $coverageRoot 'report'
$settings = Join-Path $root 'coverage.settings.xml'

if (-not (Get-Command reportgenerator -ErrorAction SilentlyContinue)) {
    throw 'reportgenerator was not found. Install it with: dotnet tool install -g dotnet-reportgenerator-globaltool'
}

# Start clean so the report never mixes in results from an earlier run.
if (Test-Path $coverageRoot) {
    Remove-Item -Recurse -Force $coverageRoot
}

Push-Location $root
try {
    # Each test project writes its own Cobertura file into its TestResults folder;
    # --results-directory gathers them all under coverage/raw.
    dotnet test --coverage --coverage-output-format cobertura --coverage-settings $settings --results-directory $rawDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed with exit code $LASTEXITCODE; no report was built."
    }

    $reports = Get-ChildItem -Path $rawDir -Recurse -Filter '*.cobertura.xml'
    if (-not $reports) {
        throw "No Cobertura files were found under $rawDir."
    }

    reportgenerator `
        "-reports:$($reports.FullName -join ';')" `
        "-targetdir:$reportDir" `
        '-reporttypes:Html;TextSummary' `
        "-title:jira-cli"
    if ($LASTEXITCODE -ne 0) {
        throw "reportgenerator failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

Get-Content (Join-Path $reportDir 'Summary.txt') -TotalCount 20

$index = Join-Path $reportDir 'index.html'
Write-Host "`nReport: $index"
if (-not $NoOpen) {
    Invoke-Item $index
}
