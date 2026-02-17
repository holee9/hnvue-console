#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Protobuf code generation helper script

.DESCRIPTION
    SPEC-INFRA-001: Protobuf code generation for C++ and C#
    Generates gRPC stubs from .proto definitions

.EXAMPLE
    .\scripts\generate-proto.ps1
    Generate protobuf code for both C++ and C#
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

function Write-SectionHeader {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptRoot
$ProtoDir = Join-Path $ProjectRoot "proto"

Write-SectionHeader "Protobuf Code Generation"

# Check protoc is available
$ProtocCmd = Get-Command protoc -ErrorAction SilentlyContinue
if (-not $ProtocCmd) {
    Write-Error "protoc not found. Please install Protocol Buffers compiler."
    exit 1
}

Write-Host "✓ protoc found at: $($ProtocCmd.Source)" -ForegroundColor Green

# Find all .proto files
$ProtoFiles = Get-ChildItem -Path $ProtoDir -Filter "*.proto" -File

if ($ProtoFiles.Count -eq 0) {
    Write-Warning "No .proto files found in $ProtoDir"
    exit 0
}

Write-Host "Found $($ProtoFiles.Count) proto file(s)" -ForegroundColor Yellow

# Generate C++ stubs
Write-SectionHeader "Generating C++ Stubs"

foreach ($ProtoFile in $ProtoFiles) {
    Write-Host "Processing: $($ProtoFile.Name)"

    # C++ generation
    $CppOutDir = Join-Path $ProjectRoot "build\proto"
    $ProtocArgs = @(
        "--proto_path=`"$ProtoDir`""
        "--cpp_out=`"$CppOutDir`""
        "--grpc_out=`"$CppOutDir`""
        "--plugin=protoc-gen-grpc=`"${env:USERPROFILE}\.vcpkg-root\installed\x64-windows\tools\grpc\grpc_cpp_plugin.exe`""
        "`"$ProtoFile.FullName`""
    )

    & protoc $ProtocArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to generate C++ stubs from $($ProtoFile.Name)"
        exit 1
    }

    Write-Host "✓ Generated C++ stubs" -ForegroundColor Green
}

# C# stubs are generated automatically by Grpc.Tools during build
Write-SectionHeader "C# Stubs"
Write-Host "C# stubs will be generated automatically during dotnet build" -ForegroundColor Yellow
Write-Host "via Grpc.Tools in HnVue.Ipc.Client.csproj" -ForegroundColor Yellow

Write-SectionHeader "Generation Complete"
exit 0
