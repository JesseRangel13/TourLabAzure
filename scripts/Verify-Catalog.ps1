[CmdletBinding()]
param([ValidateSet('LocalDb','Docker')][string]$Profile = 'LocalDb')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root = Split-Path $PSScriptRoot
$runId = [Guid]::NewGuid().ToString('N')
$directory = ".local/catalog-$runId"
$previousConfig = $env:AZURELAB_INTEGRATION_CONFIG
Push-Location $root
try {
    & "$PSScriptRoot/Initialize-SqlIdentity.ps1" -Profile $Profile -OutputDirectory $directory -Database "tourlab_azure_catalog_$runId"
    $env:AZURELAB_INTEGRATION_CONFIG = Join-Path $root "$directory/provisioner.json"
    & dotnet test tests/Scaffold.Tests -c Release --no-build --logger "trx;LogFileName=AZ-003-$runId.trx"
    if ($LASTEXITCODE -ne 0) { throw 'Catalog/SQL/security regression failed; preserve database and logs.' }
    Write-Host "Catalog/SQL suite passed. Configuration retained at $directory; use it for Verify-HttpsBff.ps1. No data deleted."
} finally {
    $env:AZURELAB_INTEGRATION_CONFIG = $previousConfig
    Pop-Location
}