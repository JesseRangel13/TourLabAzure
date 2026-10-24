[CmdletBinding()]
param([ValidateSet('LocalDb','Docker')][string]$Profile = 'LocalDb')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root = Split-Path $PSScriptRoot
$runId = [Guid]::NewGuid().ToString('N')
$directory = ".local/verify-$runId"
Push-Location $root
$previousConfig = $env:AZURELAB_INTEGRATION_CONFIG
try {
    & "$PSScriptRoot/Initialize-SqlIdentity.ps1" -Profile $Profile -OutputDirectory $directory -Database "tourlab_azure_verify_$runId"
    $env:AZURELAB_INTEGRATION_CONFIG = Join-Path $root "$directory/provisioner.json"
    & dotnet test tests/Scaffold.Tests -c Release --no-build --logger "trx;LogFileName=AZ-002-$runId.trx"
    if ($LASTEXITCODE -ne 0) { throw 'Real SQL/Identity checks failed; retain this run database and report.' }
    Write-Host "Real SQL/Identity suite passed. Verification database/config retained at $directory; no data deleted."
} finally { $env:AZURELAB_INTEGRATION_CONFIG = $previousConfig; Pop-Location }
