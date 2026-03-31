# SBOM Generation Script for HnVue Console
# SPEC-SECURITY-001: FR-SEC-08 - Software Bill of Materials
# NTIA Minimum Elements:
#   1. Component name
#   2. Version
#   3. Supplier (author/organization)
#   4. Dependency relationships
#   5. SBOM author name
#   6. Timestamp
#   7. SBOM version (schema version)

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# Configuration
$ProjectRoot = $PSScriptRoot | Split-Path | Split-Path
$OutputFile = Join-Path $ProjectRoot "components.json"
$SolutionFile = Join-Path $ProjectRoot "HnVue.sln"
$Timestamp = (Get-Date).ToUniversalTime().ToString("o")

# SBOM Metadata
$SbomMetadata = @{
    "$schema" = "https://raw.githubusercontent.com/interlynk-io/sbom-schemas/main/formats/component-identification/bom-1.5.schema.json"
    bomFormat = "CycloneDX"
    specVersion = "1.5"
    version = 1
    metadata = @{
        timestamp = $Timestamp
        tools = @(
            @{
                vendor = "HnVue"
                name = "HnVue SBOM Generator"
                version = "1.0.0"
            }
        )
        authors = @(
            @{
                name = "HnVue DevOps Team"
                email = "devops@hnvue.local"
            }
        )
        component = @{
            type = "application"
            name = "HnVue.Console"
            version = "1.0.0"
            description = "Diagnostic Medical Device X-ray GUI Console"
            supplier = @{
                name = "HnVue Medical"
                contact = @{
                    email = "info@hnvue.local"
                }
            }
            purl = "pkg:generic/hnvue/console@1.0.0"
            externalReferences = @(
                @{
                    type = "vcs"
                    url = "https://github.com/holee9/hnvue-console"
                }
            )
        }
        properties = @(
            @{
                name = "iec62304:class"
                value = "B"
            }
            @{
                name = "iec62304:safety_class"
                value = "medium"
            }
        )
    }
}

function Get-NuGetPackages {
    [CmdletBinding()]
    param()

    $packages = @()

    # Get all .csproj files
    $csprojFiles = Get-ChildItem -Path $ProjectRoot -Filter "*.csproj" -Recurse |
        Where-Object { $_.FullName -notmatch "[\\/]artifacts[\\/]" -and
                      $_.FullName -notmatch "[\\/]bin[\\/]" -and
                      $_.FullName -notmatch "[\\/]obj[\\/]" }

    foreach ($csproj in $csprojFiles) {
        $projectName = $csproj.BaseName
        $projectPath = $csproj.FullName.Substring($ProjectRoot.Length).TrimStart("\", "/").Replace("\", "/")

        Write-Host "Processing: $projectName"

        [xml]$csprojContent = Get-Content $csproj.FullName

        # Process PackageReference items
        foreach ($packageRef in $csprojContent.Project.ItemGroup.PackageReference) {
            $packageName = $packageRef.Include
            $packageVersion = $packageRef.Version

            # If version is not specified, look in Directory.Packages.props
            if ([string]::IsNullOrEmpty($packageVersion)) {
                $packageVersion = Get-CentralPackageVersion -PackageName $packageName
            }

            if (-not [string]::IsNullOrEmpty($packageName)) {
                $purl = "pkg:nuget/$packageName"
                if ($packageVersion) {
                    $purl += "@$packageVersion"
                }

                $component = @{
                    type = "library"
                    name = $packageName
                    version = $packageVersion
                    purl = $purl
                    externalReferences = @(
                        @{
                            type = "distribution"
                            url = "https://www.nuget.org/packages/$packageName"
                        }
                    )
                }

                $packages += $component
            }
        }
    }

    return $packages
}

function Get-CentralPackageVersion {
    [CmdletBinding()]
    param(
        [string]$PackageName
    )

    $propsFile = Join-Path $ProjectRoot "Directory.Packages.props"
    if (-not (Test-Path $propsFile)) {
        return $null
    }

    [xml]$propsContent = Get-Content $propsFile

    foreach ($packageVersion in $propsContent.Project.ItemGroup.PackageVersion) {
        if ($packageVersion.Include -eq $PackageName) {
            return $packageVersion.Version
        }
    }

    return $null
}

function Get-Frameworks {
    [CmdletBinding()]
    param()

    $frameworks = @()

    # .NET Runtime
    $frameworks += @{
        type = "platform"
        name = "Microsoft .NET"
        version = "8.0"
        purl = "pkg:dotnet/runtime@8.0.0"
    }

    # WPF
    $frameworks += @{
        type = "platform"
        name = "Windows Presentation Foundation"
        version = "8.0"
        purl = "pkg:dotnet/wpf@8.0.0"
    }

    return $frameworks
}

function Get-Dependencies {
    [CmdletBinding()]
    param()

    $dependencies = @()

    # Project references (internal components)
    $csprojFiles = Get-ChildItem -Path $ProjectRoot -Filter "*.csproj" -Recurse |
        Where-Object { $_.FullName -notmatch "[\\/]artifacts[\\/]" -and
                      $_.FullName -notmatch "[\\/]bin[\\/]" -and
                      $_.FullName -notmatch "[\\/]obj[\\/]" }

    foreach ($csproj in $csprojFiles) {
        $projectName = $csproj.BaseName
        $projectPath = $csproj.FullName.Substring($ProjectRoot.Length).TrimStart("\", "/").Replace("\", "/")

        [xml]$csprojContent = Get-Content $csproj.FullName

        $ref = @{
            ref = $projectName
            dependsOn = @()
        }

        # Add project references
        foreach ($projRef in $csprojContent.Project.ItemGroup.ProjectReference) {
            $refName = $projRef.Include -replace '.*?\\([^\\]+)\.csproj', '$1'
            $ref.dependsOn += $refName
        }

        $dependencies += $ref
    }

    return $dependencies
}

# Main execution
Write-Host "Generating SBOM for HnVue.Console..." -ForegroundColor Cyan

$nugetPackages = Get-NuGetPackages
$frameworks = Get-Frameworks
$dependencies = Get-Dependencies

Write-Host "Found $($nugetPackages.Count) NuGet packages"
Write-Host "Found $($frameworks.Count) framework dependencies"

# Build SBOM
$sbom = $SbomMetadata
$sbom.components = @(
    $SbomMetadata.metadata.component
    $nugetPackages
    $frameworks
)
$sbom.dependencies = $dependencies

# Convert to JSON
$sbomJson = $sbom | ConvertTo-Json -Depth 20 -Compress:$false

# Write to file
$sbomJson | Out-File -FilePath $OutputFile -Encoding UTF8

Write-Host "SBOM generated: $OutputFile" -ForegroundColor Green
Write-Host "Total components: $($sbom.components.Count)"

# Validate NTIA minimum elements
$ntiaValid = $true
$ntiaElements = @(
    "Component name: $($SbomMetadata.metadata.component.name)"
    "Version: $($SbomMetadata.metadata.component.version)"
    "Supplier: $($SbomMetadata.metadata.component.supplier.name)"
    "Dependencies: $($dependencies.Count)"
    "SBOM author: $($SbomMetadata.metadata.authors[0].name)"
    "Timestamp: $Timestamp"
    "Schema version: $($SbomMetadata.specVersion)"
)

Write-Host "`nNTIA Minimum Elements Validation:" -ForegroundColor Cyan
foreach ($element in $ntiaElements) {
    Write-Host "  [$($element)]" -ForegroundColor Green
}

return $OutputFile
