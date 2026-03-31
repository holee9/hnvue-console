# Development Certificate Generation Script
# SPEC-SECURITY-001: Self-signed certificates for test environment
# Usage: .\scripts\certs\generate-dev-certs.ps1

param(
    [string]$OutputPath = "certs",
    [int]$ValidDays = 365,
    [string]$Password = "DevPassword123!"
)

$ErrorActionPreference = "Stop"

Write-Host "=== HnVue Console - Development Certificate Generator ===" -ForegroundColor Cyan

# Ensure output directory exists
if (!(Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Host "[OK] Created directory: $OutputPath" -ForegroundColor Green
}

# Certificate Distinguished Name properties
$CaDistinguishedName = "CN=HnVue-Dev-CA,O=HnVue,C=KR"
$ServerDistinguishedName = "CN=localhost,O=HnVue,C=KR"
$ClientDistinguishedName = "CN=HnVue-Console-Client,O=HnVue,C=KR"

# 1. Create Root CA Certificate
Write-Host "`n[1/3] Creating Root CA Certificate..." -ForegroundColor Yellow
$caCert = New-SelfSignedCertificate `
    -Type Custom `
    -DnsName "HnVue-Dev-CA" `
    -Subject $CaDistinguishedName `
    -KeyUsage DigitalSignature,CertSign `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -NotAfter (Get-Date).AddDays($ValidDays)

# Export CA certificate (public key)
$caCertPath = Join-Path $OutputPath "ca.crt"
Export-Certificate -Cert $caCert -FilePath $caCertPath -Type CERT | Out-Null
Write-Host "[OK] Root CA exported to: $caCertPath" -ForegroundColor Green

# 2. Create Server Certificate (signed by CA)
Write-Host "`n[2/3] Creating Server Certificate..." -ForegroundColor Yellow
$serverCert = New-SelfSignedCertificate `
    -Type Custom `
    -DnsName "localhost","127.0.0.1","hnvue.local" `
    -Subject $ServerDistinguishedName `
    -KeyUsage DigitalSignature,KeyEncipherment `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -NotAfter (Get-Date).AddDays($ValidDays)

# Export Server certificate with private key (PFX)
$serverCertPath = Join-Path $OutputPath "server.pfx"
$serverCertPassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $serverCert -FilePath $serverCertPath -Password $serverCertPassword | Out-Null
Write-Host "[OK] Server certificate exported to: $serverCertPath" -ForegroundColor Green

# 3. Create Client Certificate (signed by CA)
Write-Host "`n[3/3] Creating Client Certificate..." -ForegroundColor Yellow
$clientCert = New-SelfSignedCertificate `
    -Type Custom `
    -DnsName "HnVue-Console-Client" `
    -Subject $ClientDistinguishedName `
    -KeyUsage DigitalSignature `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -NotAfter (Get-Date).AddDays($ValidDays)

# Export Client certificate with private key (PFX)
$clientCertPath = Join-Path $OutputPath "client.pfx"
$clientCertPassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $clientCert -FilePath $clientCertPath -Password $clientCertPassword | Out-Null
Write-Host "[OK] Client certificate exported to: $clientCertPath" -ForegroundColor Green

# Display certificate thumbprints for validation
Write-Host "`n=== Certificate Details ===" -ForegroundColor Cyan
Write-Host "Root CA Thumbprint: $($caCert.Thumbprint)" -ForegroundColor White
Write-Host "Server Thumbprint:  $($serverCert.Thumbprint)" -ForegroundColor White
Write-Host "Client Thumbprint:  $($clientCert.Thumbprint)" -ForegroundColor White

# Configuration snippet for appsettings.json
Write-Host "`n=== Configuration Snippet ===" -ForegroundColor Cyan
Write-Host @"
Add to your appsettings.Development.json:
{
  "GrpcSecurity": {
    "ClientCertificatePath": "$clientCertPath",
    "ClientCertificatePassword": "$Password",
    "RootCertificatePath": "$caCertPath"
  }
}
"@ -ForegroundColor White

# Cleanup: Remove certificates from local machine store
Write-Host "`n[Cleanup] Removing certificates from local store..." -ForegroundColor Yellow
Remove-Item -Path "Cert:\LocalMachine\My\$($caCert.Thumbprint)" -Force
Remove-Item -Path "Cert:\LocalMachine\My\$($serverCert.Thumbprint)" -Force
Remove-Item -Path "Cert:\LocalMachine\My\$($clientCert.Thumbprint)" -Force
Write-Host "[OK] Certificates removed from store (file backups retained)" -ForegroundColor Green

Write-Host "`n=== Certificate Generation Complete ===" -ForegroundColor Green
Write-Host "Files created in: $OutputPath" -ForegroundColor White
Write-Host "  - ca.crt      (Root CA public certificate)" -ForegroundColor White
Write-Host "  - server.pfx  (Server certificate with private key)" -ForegroundColor White
Write-Host "  - client.pfx  (Client certificate with private key)" -ForegroundColor White
Write-Host "`nWARNING: These are self-signed certificates for DEVELOPMENT ONLY!" -ForegroundColor Red
Write-Host "         Do NOT use in production environments." -ForegroundColor Red
