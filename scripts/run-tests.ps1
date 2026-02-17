#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Local test runner script for HnVue project

.DESCRIPTION
    SPEC-INFRA-001 FR-INFRA-05.8: Local test runner
    Executes C++ tests via CTest and C# tests via dotnet test

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release

.PARAMETER SkipCpp
    Skip C++ tests

.PARAMETER SkipCsharp
    Skip C# tests

.PARAMETER SkipIntegration
    Skip integration tests (requires Docker)

.EXAMPLE
    .\scripts\run-tests.ps1
    Run all tests in Release mode

.EXAMPLE
    .\scripts\run-tests.ps1 -Configuration Debug
    Run all tests in Debug mode
#>

[CmdletBinding()]
param(
    [Parameter(Position=0)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$SkipCpp,

    [switch]$SkipCsharp,

    [switch]$SkipIntegration
)

$ErrorActionPreference = "Stop"

function Write-SectionHeader {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptRoot

$TotalTests = 0
$FailedTests = 0

# Run C++ tests
if (-not $SkipCpp) {
    Write-SectionHeader "Running C++ Tests (CTest)"

    $CMakePreset = if ($Configuration -eq "Debug") { "debug" } else { "release" }

    try {
        Push-Location $ProjectRoot
        $TestResults = ctest --preset $CMakePreset --output-on-failure 2>&1
        $CppExitCode = $LASTEXITCODE

        if ($CppExitCode -eq 0) {
            Write-Host "✓ C++ tests passed" -ForegroundColor Green
        }
        else {
            Write-Error "C++ tests failed with exit code: $CppExitCode"
            $FailedTests++
        }
    }
    finally {
        Pop-Location
    }
}

# Run C# tests
if (-not $SkipCsharp) {
    Write-SectionHeader "Running C# Unit Tests (xUnit)"

    $SolutionFile = Join-Path $ProjectRoot "HnVue.sln"

    try {
        Push-Location $ProjectRoot
        $TestResults = dotnet test $SolutionFile -c $Configuration --no-build `
            --logger "console;verbosity=detailed" `
            --logger "trx;LogFileName=test-results.trx" `
            -- DataCollectionRunSettings.DataCollectors="Code Coverage"
        $CSharpExitCode = $LASTEXITCODE

        if ($CSharpExitCode -eq 0) {
            Write-Host "✓ C# tests passed" -ForegroundColor Green
        }
        else {
            Write-Error "C# tests failed with exit code: $CSharpExitCode"
            $FailedTests++
        }
    }
    finally {
        Pop-Location
    }
}

# Run integration tests
if (-not $SkipIntegration) {
    Write-SectionHeader "Running Integration Tests (with Orthanc)"

    $DockerCompose = Join-Path $ProjectRoot "tests\docker\docker-compose.orthanc.yml"

    try {
        # Start Orthanc container
        Write-Host "Starting Orthanc DICOM server..."
        docker-compose -f $DockerCompose up -d

        # Wait for health check
        Write-Host "Waiting for Orthanc to be healthy..."
        $MaxRetries = 12
        $RetryCount = 0
        $Healthy = $false

        while (-not $Healthy -and $RetryCount -lt $MaxRetries) {
            Start-Sleep -Seconds 5
            $HealthCheck = docker-compose -f $DockerCompose ps | Select-String "healthy"

            if ($HealthCheck) {
                $Healthy = $true
                Write-Host "✓ Orthanc is healthy" -ForegroundColor Green
            }

            $RetryCount++
        }

        if (-not $Healthy) {
            Write-Error "Orthanc failed to become healthy"
            $FailedTests++
        }
        else {
            # Run integration tests
            $IntegrationTestProject = Join-Path $ProjectRoot "tests\integration\HnVue.Integration.Tests\HnVue.Integration.Tests.csproj"

            $TestResults = dotnet test $IntegrationTestProject -c $Configuration --no-build
            $IntegrationExitCode = $LASTEXITCODE

            if ($IntegrationExitCode -ne 0) {
                Write-Error "Integration tests failed"
                $FailedTests++
            }
        }
    }
    finally {
        # Stop Orthanc container
        Write-Host "Stopping Orthanc DICOM server..."
        docker-compose -f $DockerCompose down
    }
}

Write-SectionHeader "Test Summary"

if ($FailedTests -eq 0) {
    Write-Host "All tests passed!" -ForegroundColor Green
    exit 0
}
else {
    Write-Error "$FailedTests test suite(s) failed"
    exit 1
}
