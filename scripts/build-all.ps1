#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Unified build script for HnVue project

.DESCRIPTION
    SPEC-INFRA-001 FR-INFRA-01.6: Unified build entry point
    Builds C++ libraries via CMake, then C# projects via MSBuild
    in correct dependency order

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release

.PARAMETER SkipCpp
    Skip C++ build

.PARAMETER SkipCsharp
    Skip C# build

.EXAMPLE
    .\scripts\build-all.ps1
    Build all components in Release mode

.EXAMPLE
    .\scripts\build-all.ps1 -Configuration Debug
    Build all components in Debug mode
#>

[CmdletBinding()]
param(
    [Parameter(Position=0)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$SkipCpp,

    [switch]$SkipCsharp
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Write-SectionHeader {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Test-Command {
    param([string]$Command)
    $null = Get-Command $Command -ErrorAction SilentlyContinue
    return $?
}

# Project root
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptRoot

Write-SectionHeader "HnVue Build Script"
Write-Host "Project Root: $ProjectRoot"
Write-Host "Configuration: $Configuration"

# Validate required tools
Write-SectionHeader "Validating Tools"

$RequiredTools = @{
    "cmake" = "CMake 3.25+"
    "dotnet" = ".NET 8 SDK"
}

foreach ($tool in $RequiredTools.Keys) {
    if (-not (Test-Command $tool)) {
        Write-Error "Missing required tool: $($RequiredTools[$tool])"
        exit 1
    }
    Write-Host "✓ $tool found" -ForegroundColor Green
}

# Build C++ libraries
if (-not $SkipCpp) {
    Write-SectionHeader "Building C++ Libraries (CMake)"

    $CMakeBuildDir = Join-Path $ProjectRoot "build"
    $CMakePreset = if ($Configuration -eq "Debug") { "debug" } else { "release" }

    try {
        # Configure
        Write-Host "Configuring CMake with preset: $CMakePreset"
        cmake --preset $CMakePreset

        # Build
        Write-Host "Building C++ target: all-libs"
        cmake --build --preset $CMakePreset --target all-libs --config $Configuration

        Write-Host "✓ C++ build completed" -ForegroundColor Green
    }
    catch {
        Write-Error "C++ build failed: $_"
        exit 1
    }
}

# Build C# projects
if (-not $SkipCsharp) {
    Write-SectionHeader "Building C# Projects (MSBuild)"

    $SolutionFile = Join-Path $ProjectRoot "HnVue.sln"

    try {
        Write-Host "Building solution: $SolutionFile"
        dotnet build $SolutionFile -c $Configuration --no-restore

        Write-Host "✓ C# build completed" -ForegroundColor Green
    }
    catch {
        Write-Error "C# build failed: $_"
        exit 1
    }
}

Write-SectionHeader "Build Complete"
Write-Host "All components built successfully!" -ForegroundColor Green
exit 0
