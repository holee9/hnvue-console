# NTIA Minimum Elements Validation Script
# Validates that components.json meets NTIA minimum element requirements

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot | Split-Path | Split-Path
$SbomFile = Join-Path $ProjectRoot "components.json"

if (-not (Test-Path $SbomFile)) {
    Write-Error "SBOM file not found: $SbomFile"
    exit 1
}

$sbom = Get-Content $SbomFile | ConvertFrom-Json

# NTIA Minimum Elements (per NTIA Memo "The Minimum Elements for a Software Bill of Materials (SBOM)")
# 1. All component names
# 2. All versions of all components
# 3. All suppliers (authors/organizations) of all components
# 4. Dependency relationships (including "top-level" vs "transitive")
# 5. SBOM author name
# 6. SBOM creation timestamp
# 7. SBOM version (schema version)

Write-Host "Validating NTIA Minimum Elements..." -ForegroundColor Cyan

$errors = @()
$warnings = @()

# 1. Component Names
if (-not $sbom.metadata.component.name) {
    $errors += "Missing primary component name"
}
foreach ($component in $sbom.components) {
    if (-not $component.name) {
        $errors += "Component missing name: $($component | ConvertTo-Json -Compress)"
    }
}

# 2. Versions
if (-not $sbom.metadata.component.version) {
    $errors += "Missing primary component version"
}
$versionless = $sbom.components | Where-Object { $_.type -eq "library" -and -not $_.version }
if ($versionless) {
    $warnings += "Components without version: $($versionless.Count)"
}

# 3. Supplier
if (-not $sbom.metadata.component.supplier) {
    $errors += "Missing primary component supplier"
}

# 4. Dependency Relationships
if (-not $sbom.dependencies -or $sbom.dependencies.Count -eq 0) {
    $warnings += "No dependency relationships defined"
}

# 5. SBOM Author
if (-not $sbom.metadata.authors -or $sbom.metadata.authors.Count -eq 0) {
    $errors += "Missing SBOM author"
}

# 6. Timestamp
if (-not $sbom.metadata.timestamp) {
    $errors += "Missing SBOM timestamp"
}

# 7. Schema Version
if (-not $sbom.specVersion) {
    $errors += "Missing SBOM schema version"
}

# Report results
Write-Host "`n=== Validation Results ===" -ForegroundColor Cyan

if ($errors.Count -eq 0 -and $warnings.Count -eq 0) {
    Write-Host "PASS: All NTIA minimum elements present" -ForegroundColor Green
    Write-Host "  - Component names: $($sbom.components.Count)"
    Write-Host "  - Versions: Specified"
    Write-Host "  - Supplier: $($sbom.metadata.component.supplier.name)"
    Write-Host "  - Dependencies: $($sbom.dependencies.Count) relationships"
    Write-Host "  - Author: $($sbom.metadata.authors[0].name)"
    Write-Host "  - Timestamp: $($sbom.metadata.timestamp)"
    Write-Host "  - Schema: $($sbom.specVersion)"
    exit 0
} else {
    if ($errors.Count -gt 0) {
        Write-Host "`nERRORS:" -ForegroundColor Red
        foreach ($error in $errors) {
            Write-Host "  - $error" -ForegroundColor Red
        }
    }

    if ($warnings.Count -gt 0) {
        Write-Host "`nWARNINGS:" -ForegroundColor Yellow
        foreach ($warning in $warnings) {
            Write-Host "  - $warning" -ForegroundColor Yellow
        }
    }

    exit 1
}
